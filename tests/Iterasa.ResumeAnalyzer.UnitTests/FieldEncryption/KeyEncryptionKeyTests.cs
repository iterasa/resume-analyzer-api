using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using Amazon.Runtime;
using FluentAssertions;
using Iterasa.ResumeAnalyzer.Infrastructure.Security.FieldEncryption;
using System.Security.Cryptography;
using Xunit;

namespace Iterasa.ResumeAnalyzer.UnitTests.FieldEncryption;

public sealed class LocalKeyEncryptionKeyTests {

    private static readonly KeyWrapContext Context = new(Guid.NewGuid(), KeyPurpose.Encryption, 1);

    [Fact]
    public async Task A_Wrapped_Key_Unwraps_With_The_Same_KEK_And_Context() {
        var kek = new LocalKeyEncryptionKeyProvider(RandomNumberGenerator.GetBytes(32));
        var dek = RandomNumberGenerator.GetBytes(32);

        var wrapped = await kek.WrapAsync(dek, Context, CancellationToken.None);

        wrapped.Should().NotContainInOrder(dek);
        (await kek.UnwrapAsync(wrapped, kek.CurrentKekId, Context, CancellationToken.None)).Should().Equal(dek);
    }

    [Fact]
    public async Task A_Key_Wrapped_For_Another_Scope_Does_Not_Unwrap() {
        var kek = new LocalKeyEncryptionKeyProvider(RandomNumberGenerator.GetBytes(32));
        var wrapped = await kek.WrapAsync(RandomNumberGenerator.GetBytes(32), Context, CancellationToken.None);

        var act = () => kek.UnwrapAsync(wrapped, kek.CurrentKekId, Context with { ScopeId = Guid.NewGuid() }, CancellationToken.None);

        await act.Should().ThrowAsync<CryptographicException>();
    }

    [Fact]
    public async Task Another_KEK_Cannot_Unwrap() {
        var kek = new LocalKeyEncryptionKeyProvider(RandomNumberGenerator.GetBytes(32));
        var other = new LocalKeyEncryptionKeyProvider(RandomNumberGenerator.GetBytes(32));
        var wrapped = await kek.WrapAsync(RandomNumberGenerator.GetBytes(32), Context, CancellationToken.None);

        var act = () => other.UnwrapAsync(wrapped, kek.CurrentKekId, Context, CancellationToken.None);

        await act.Should().ThrowAsync<CryptographicException>();
    }

}

public sealed class AwsKmsKeyEncryptionKeyTests {

    [Fact]
    public async Task Wraps_With_The_Configured_Key_And_Unwraps_With_The_Recorded_One_Under_The_Same_Context() {
        var kms = new FakeKms();
        var provider = new AwsKmsKeyEncryptionKeyProvider(kms, "arn:aws:kms:us-east-1:111122223333:key/current");
        var context = new KeyWrapContext(Guid.NewGuid(), KeyPurpose.BlindIndex, 3);
        var dek = RandomNumberGenerator.GetBytes(32);

        var wrapped = await provider.WrapAsync(dek, context, CancellationToken.None);
        var unwrapped = await provider.UnwrapAsync(wrapped, "arn:aws:kms:us-east-1:111122223333:key/old", context, CancellationToken.None);

        unwrapped.Should().Equal(dek);
        kms.EncryptedWith.Should().Be("arn:aws:kms:us-east-1:111122223333:key/current");
        kms.DecryptedWith.Should().Be("arn:aws:kms:us-east-1:111122223333:key/old");
        kms.Contexts.Should().AllSatisfy(sent => sent.Should().BeEquivalentTo(new Dictionary<string, string> {
            ["app"] = "criba",
            ["scope"] = context.ScopeId.ToString("D"),
            ["purpose"] = "idx",
            ["version"] = "3",
        }));
    }

    /// <summary>KMS simulado: «cifra» invirtiendo los bytes y guarda lo que recibió.</summary>
    private sealed class FakeKms() : AmazonKeyManagementServiceClient(new AnonymousAWSCredentials(), Amazon.RegionEndpoint.USEast1) {

        public string? EncryptedWith { get; private set; }
        public string? DecryptedWith { get; private set; }
        public List<Dictionary<string, string>> Contexts { get; } = [];

        public override Task<EncryptResponse> EncryptAsync(EncryptRequest request, CancellationToken cancellationToken = default) {
            EncryptedWith = request.KeyId;
            Contexts.Add(request.EncryptionContext);
            var blob = request.Plaintext.ToArray().Reverse().ToArray();
            return Task.FromResult(new EncryptResponse { CiphertextBlob = new MemoryStream(blob), KeyId = request.KeyId });
        }

        public override Task<DecryptResponse> DecryptAsync(DecryptRequest request, CancellationToken cancellationToken = default) {
            DecryptedWith = request.KeyId;
            Contexts.Add(request.EncryptionContext);
            var plain = request.CiphertextBlob.ToArray().Reverse().ToArray();
            return Task.FromResult(new DecryptResponse { Plaintext = new MemoryStream(plain), KeyId = request.KeyId });
        }

    }

}