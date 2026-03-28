using FluentAssertions;
using GvResearch.Shared.Auth;

namespace GvResearch.Shared.Tests.Auth;

public class TokenEncryptionTests
{
    private static byte[] CreateKey() => new byte[32]; // 256-bit key of zeros for tests

    [Fact]
    public void Encrypt_ThenDecrypt_ReturnsOriginalPlaintext()
    {
        var key = CreateKey();
        const string plaintext = "my-secret-token-value";

        var ciphertext = TokenEncryption.Encrypt(plaintext, key);
        var result = TokenEncryption.Decrypt(ciphertext, key);

        result.Should().Be(plaintext);
    }

    [Fact]
    public void Encrypt_CalledTwice_ProducesDifferentCiphertext()
    {
        var key = CreateKey();
        const string plaintext = "same-token";

        var cipher1 = TokenEncryption.Encrypt(plaintext, key);
        var cipher2 = TokenEncryption.Encrypt(plaintext, key);

        cipher1.Should().NotEqual(cipher2);
    }

    [Fact]
    public void Decrypt_WithWrongKey_ThrowsCryptographicException()
    {
        var key = CreateKey();
        var wrongKey = new byte[32];
        wrongKey[0] = 0xFF;

        var ciphertext = TokenEncryption.Encrypt("secret", key);
        var act = () => TokenEncryption.Decrypt(ciphertext, wrongKey);

        act.Should().Throw<System.Security.Cryptography.CryptographicException>();
    }

    [Fact]
    public void Encrypt_EmptyString_RoundTripsSuccessfully()
    {
        var key = CreateKey();
        var ciphertext = TokenEncryption.Encrypt(string.Empty, key);
        var result = TokenEncryption.Decrypt(ciphertext, key);
        result.Should().Be(string.Empty);
    }
}
