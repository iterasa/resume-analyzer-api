using FluentAssertions;
using Iterasa.ResumeAnalyzer.Infrastructure.Security.FieldEncryption;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using System.Security.Cryptography;
using Xunit;

namespace Iterasa.ResumeAnalyzer.UnitTests.FieldEncryption;

public sealed class FieldEncryptionOptionsValidatorTests {

    private static readonly string LocalKek = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void The_Local_KEK_Is_Refused_Outside_Development_And_Testing(string environment) {
        var result = Validate(environment, new FieldEncryptionOptions { KekProvider = "local", LocalKek = LocalKek });
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("aws-kms");
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    public void The_Local_KEK_Is_Allowed_In_Development_And_Testing(string environment) =>
        Validate(environment, new FieldEncryptionOptions { KekProvider = "local", LocalKek = LocalKek }).Succeeded.Should().BeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("none")]
    public void Without_A_KEK_The_Application_Does_Not_Start(string? provider) =>
        Validate("Production", new FieldEncryptionOptions { KekProvider = provider }).Failed.Should().BeTrue();

    [Fact]
    public void KMS_Needs_A_Key_Id() {
        Validate("Production", new FieldEncryptionOptions { KekProvider = "aws-kms" }).Failed.Should().BeTrue();
        Validate("Production", new FieldEncryptionOptions { KekProvider = "aws-kms", KmsKeyId = "alias/criba-pii" }).Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData("not-base64")]
    [InlineData("AAAA")]
    public void The_Local_KEK_Must_Be_32_Bytes(string value) =>
        Validate("Development", new FieldEncryptionOptions { KekProvider = "local", LocalKek = value }).Failed.Should().BeTrue();

    private static Microsoft.Extensions.Options.ValidateOptionsResult Validate(string environment, FieldEncryptionOptions options) =>
        new FieldEncryptionOptionsValidator(new FakeEnvironment(environment)).Validate(null, options);

    private sealed class FakeEnvironment(string name) : IHostEnvironment {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = "/";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

}

public sealed class KeyRingCacheTests {

    private static readonly Guid Scope = Guid.NewGuid();

    [Fact]
    public async Task Concurrent_Callers_Share_One_Load() {
        var rings = new TestRings().WithScope(Scope);
        var cache = rings.Cache();

        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => cache.GetAsync(Scope, CancellationToken.None).AsTask()));

        rings.Loads.Should().Be(1);
    }

    [Fact]
    public async Task An_Expired_Ring_Is_Loaded_Again() {
        var rings = new TestRings().WithScope(Scope);
        var clock = new ManualClock(DateTimeOffset.UnixEpoch);
        var cache = rings.Cache(clock);

        await cache.GetAsync(Scope, CancellationToken.None);
        clock.Advance(KeyRingCache.TimeToLive - TimeSpan.FromSeconds(1));
        await cache.GetAsync(Scope, CancellationToken.None);
        clock.Advance(TimeSpan.FromSeconds(2));
        await cache.GetAsync(Scope, CancellationToken.None);

        rings.Loads.Should().Be(2);
    }

    [Fact]
    public async Task A_Failed_Load_Is_Not_Cached() {
        var rings = new TestRings();
        var cache = rings.Cache();

        var act = async () => await cache.GetAsync(Scope, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>();

        rings.WithScope(Scope);
        (await cache.GetAsync(Scope, CancellationToken.None)).ScopeId.Should().Be(Scope);
    }

    [Fact]
    public void A_Ring_Needs_Exactly_One_Active_Key_Per_Purpose() {
        var act = () => new KeyRing(Scope, [new KeyMaterial("a", KeyPurpose.Encryption, 1, KeyStatus.Active, new byte[32])]);
        act.Should().Throw<InvalidOperationException>().WithMessage("*0 active idx keys*");
    }

    [Fact]
    public void Retired_And_Destroyed_Keys_Are_Not_Loaded() {
        var ring = new KeyRing(Scope, [
            new KeyMaterial("enc1", KeyPurpose.Encryption, 1, KeyStatus.Destroyed, new byte[32]),
            new KeyMaterial("enc2", KeyPurpose.Encryption, 2, KeyStatus.Retired, new byte[32]),
            new KeyMaterial("enc3", KeyPurpose.Encryption, 3, KeyStatus.Active, new byte[32]),
            new KeyMaterial("idx1", KeyPurpose.BlindIndex, 1, KeyStatus.Active, new byte[32]),
        ]);
        ring.TryGet("enc1", out _).Should().BeFalse();
        ring.TryGet("enc2", out _).Should().BeFalse();
        ring.ActiveEncryptionKey.KeyId.Should().Be("enc3");
    }

}

public sealed class TenantCryptoScopeTests {

    private static readonly EncryptedField TenantField = new("candidate", "email", CryptoScopeKind.Tenant);

    [Fact]
    public void A_Tenant_Field_Without_A_Scope_Is_An_Error_Never_A_Global_Key() {
        var act = () => TenantCryptoScope.Resolve(TenantField);
        act.Should().Throw<InvalidOperationException>().WithMessage("*no tenant crypto scope*");
    }

    [Fact]
    public void A_Job_Opens_The_Scope_Of_The_Row_And_Scopes_Nest() {
        var outer = Guid.NewGuid();
        var inner = Guid.NewGuid();
        using (TenantCryptoScope.Enter(outer)) {
            TenantCryptoScope.Resolve(TenantField).Should().Be(outer);
            using (TenantCryptoScope.Enter(inner)) {
                TenantCryptoScope.Resolve(TenantField).Should().Be(inner);
            }
            TenantCryptoScope.Resolve(TenantField).Should().Be(outer);
        }
        TenantCryptoScope.Current.Should().BeNull();
    }

    [Fact]
    public void Platform_Fields_Always_Use_The_Platform_Scope() {
        using (TenantCryptoScope.Enter(Guid.NewGuid())) {
            TenantCryptoScope.Resolve(EncryptedFields.UserEmail).Should().Be(CryptoScopes.Platform);
        }
    }

    [Fact]
    public void The_Platform_Id_Cannot_Be_Used_As_A_Tenant() {
        var act = () => TenantCryptoScope.Enter(CryptoScopes.Platform);
        act.Should().Throw<ArgumentException>();
    }

}