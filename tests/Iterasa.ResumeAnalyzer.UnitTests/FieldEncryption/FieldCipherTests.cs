using FluentAssertions;
using Iterasa.ResumeAnalyzer.Infrastructure.Security.FieldEncryption;
using System.Buffers.Text;
using Xunit;

namespace Iterasa.ResumeAnalyzer.UnitTests.FieldEncryption;

public sealed class FieldCipherTests {

    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();
    private static readonly EncryptedField Email = EncryptedFields.UserEmail;
    private static readonly EncryptedField OtherColumn = new("user", "phone", CryptoScopeKind.Tenant);
    private static readonly EncryptedField OtherTable = new("candidate", "email", CryptoScopeKind.Tenant);

    private readonly TestRings rings = new TestRings().WithScope(TenantA).WithScope(TenantB);

    [Fact]
    public void A_Value_Survives_The_Round_Trip() {
        var cipher = rings.Cipher();
        var stored = cipher.Encrypt("ana@example.test", Email, TenantA);
        cipher.Decrypt(stored, Email, TenantA).Should().Be("ana@example.test");
    }

    [Fact]
    public void The_Same_Text_Gives_A_Different_Ciphertext_Each_Time() {
        var cipher = rings.Cipher();
        cipher.Encrypt("ana@example.test", Email, TenantA).Should().NotBe(cipher.Encrypt("ana@example.test", Email, TenantA));
    }

    [Fact]
    public void The_Stored_Value_Has_The_Format_And_Never_The_Text() {
        var stored = rings.Cipher().Encrypt("ana@example.test", Email, TenantA);
        stored.Should().StartWith("enc1.").And.NotContain("ana").And.NotContain("example");
        stored.Split('.').Should().HaveCount(3);
    }

    [Fact]
    public void One_Flipped_Bit_Makes_Decryption_Fail() {
        var cipher = rings.Cipher();
        var stored = cipher.Encrypt("ana@example.test", Email, TenantA);
        var parts = stored.Split('.');
        var payload = Base64Url.DecodeFromChars(parts[2]);
        payload[^20] ^= 0x01;
        var tampered = $"{parts[0]}.{parts[1]}.{Base64Url.EncodeToString(payload)}";

        var act = () => cipher.Decrypt(tampered, Email, TenantA);

        act.Should().Throw<FieldDecryptionException>().Which.Reason.Should().Be("tag");
    }

    [Fact]
    public void A_Ciphertext_Moved_To_Another_Tenant_Does_Not_Decrypt() {
        var cipher = rings.Cipher();
        var stored = cipher.Encrypt("ana@example.test", Email, TenantA);

        // La llave de A no está en el anillo de B: ni siquiera se intenta.
        var act = () => cipher.Decrypt(stored, Email, TenantB);

        act.Should().Throw<FieldDecryptionException>().Which.Reason.Should().Be("unknown_key");
    }

    [Fact]
    public void The_Same_Key_Under_Another_Tenant_Context_Does_Not_Decrypt() {
        // Aun si el anillo de B tuviera la misma llave, los datos asociados atan el valor a A.
        var shared = new KeyMaterial("shared01", KeyPurpose.Encryption, 1, KeyStatus.Active, new byte[32]);
        var stored = FieldCipher.EncryptWith("ana@example.test", Email, TenantA, shared, new byte[12]);
        var ringsWithShared = new TestRings();
        ringsWithShared.Add(TenantB, shared);
        ringsWithShared.Add(TenantB, shared with { KeyId = "idx00001", Purpose = KeyPurpose.BlindIndex });

        var act = () => ringsWithShared.Cipher().Decrypt(stored, Email, TenantB);

        act.Should().Throw<FieldDecryptionException>().Which.Reason.Should().Be("tag");
    }

    [Theory]
    [InlineData("column")]
    [InlineData("table")]
    public void A_Ciphertext_Moved_To_Another_Column_Or_Table_Does_Not_Decrypt(string moveTo) {
        var other = moveTo == "column" ? OtherColumn : OtherTable;
        var cipher = rings.Cipher();
        var stored = cipher.Encrypt("ana@example.test", Email, TenantA);

        var act = () => cipher.Decrypt(stored, other, TenantA);

        act.Should().Throw<FieldDecryptionException>().Which.Reason.Should().Be("tag");
    }

    [Fact]
    public void An_Unknown_Key_Id_Makes_Decryption_Fail() {
        var cipher = rings.Cipher();
        var stored = cipher.Encrypt("ana@example.test", Email, TenantA);
        var forged = "enc1.zzzzzzzz." + stored.Split('.')[2];

        var act = () => cipher.Decrypt(forged, Email, TenantA);

        act.Should().Throw<FieldDecryptionException>().Which.Reason.Should().Be("unknown_key");
    }

    [Theory]
    [InlineData("ana@example.test")]
    [InlineData("enc1.")]
    [InlineData("enc1.abc")]
    [InlineData("enc1.abc.!!!")]
    [InlineData("enc2.abc.AAAA")]
    public void Plain_Text_Or_An_Unknown_Format_Is_Never_Returned_As_Data(string stored) {
        var act = () => rings.Cipher().Decrypt(stored, Email, TenantA);
        act.Should().Throw<FieldDecryptionException>().Which.Reason.Should().Be("format");
    }

    [Fact]
    public void The_Error_Never_Carries_The_Value() {
        var cipher = rings.Cipher();
        var stored = cipher.Encrypt("ana@example.test", Email, TenantA);

        var exception = Assert.Throws<FieldDecryptionException>(() => cipher.Decrypt(stored, Email, TenantB));

        exception.Message.Should().NotContain("ana").And.NotContain(stored.Split('.')[2]);
    }

    [Fact]
    public void A_Rotated_Key_Still_Decrypts_What_It_Encrypted_And_New_Values_Use_The_New_Key() {
        var cache = rings.Cache();
        var cipher = rings.Cipher(cache);
        var before = cipher.Encrypt("ana@example.test", Email, TenantA);

        var next = rings.Rotate(TenantA, KeyPurpose.Encryption);
        cache.Invalidate(TenantA);
        var after = cipher.Encrypt("ana@example.test", Email, TenantA);

        FieldCipher.KeyIdOf(after).Should().Be(next.KeyId);
        FieldCipher.KeyIdOf(before).Should().NotBe(next.KeyId);
        cipher.Decrypt(before, Email, TenantA).Should().Be("ana@example.test");
        cipher.Decrypt(after, Email, TenantA).Should().Be("ana@example.test");
    }

    /// <summary>
    /// Vector fijo calculado con otra implementación (Node.js crypto): detecta un cambio
    /// accidental del formato, los datos asociados o el algoritmo.
    /// </summary>
    [Fact]
    public void A_Known_Key_And_Nonce_Give_The_Known_Output() {
        var key = new KeyMaterial("testkey0", KeyPurpose.Encryption, 1, KeyStatus.Active, Enumerable.Range(0, 32).Select(i => (byte)i).ToArray());
        var nonce = Enumerable.Range(0, 12).Select(i => (byte)(0xa0 + i)).ToArray();
        var scope = Guid.Parse("11111111-1111-1111-1111-111111111111");

        FieldCipher.EncryptWith("ana@example.test", Email, scope, key, nonce)
            .Should().Be("enc1.testkey0.oKGio6Slpqeoqaqrh3YdbSCzY9ISCeL9cx-zqkQ1ZKbVBcIwm8xRVTfKfQQ");
    }

}