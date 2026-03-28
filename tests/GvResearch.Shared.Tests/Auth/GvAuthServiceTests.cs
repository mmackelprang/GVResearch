using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using GvResearch.Shared.Auth;

namespace GvResearch.Shared.Tests.Auth;

public sealed class GvAuthServiceTests
{
    [Fact]
    public void ComputeSapisidHash_ReturnsCorrectFormat()
    {
        var cookiePath = Path.GetTempFileName();
        var keyPath = Path.GetTempFileName();
        try
        {
            var key = new byte[32];
            RandomNumberGenerator.Fill(key);
            var cookies = new GvCookieSet
            {
                Sapisid = "test-sapisid",
                Sid = "test-sid",
                Hsid = "test-hsid",
                Ssid = "test-ssid",
                Apisid = "test-apisid"
            };
            File.WriteAllBytes(keyPath, key);
            File.WriteAllBytes(cookiePath, TokenEncryption.Encrypt(cookies.Serialize(), key));

            var sut = new GvAuthService(cookiePath, keyPath);

            var result = sut.ComputeSapisidHash("test-sapisid", "https://voice.google.com");

            result.Should().StartWith("SAPISIDHASH ");
            var parts = result["SAPISIDHASH ".Length..].Split('_');
            parts.Should().HaveCount(2);
            long.TryParse(parts[0], out _).Should().BeTrue("timestamp should be numeric");
            parts[1].Should().HaveLength(40, "SHA1 hex digest is 40 chars");
        }
        finally
        {
            File.Delete(cookiePath);
            File.Delete(keyPath);
        }
    }

    [Fact]
    public void ComputeSapisidHash_IsCorrectSha1()
    {
        var cookiePath = Path.GetTempFileName();
        var keyPath = Path.GetTempFileName();
        try
        {
            var key = new byte[32];
            RandomNumberGenerator.Fill(key);
            var cookies = new GvCookieSet
            {
                Sapisid = "abc123",
                Sid = "s",
                Hsid = "h",
                Ssid = "ss",
                Apisid = "a"
            };
            File.WriteAllBytes(keyPath, key);
            File.WriteAllBytes(cookiePath, TokenEncryption.Encrypt(cookies.Serialize(), key));

            var sut = new GvAuthService(cookiePath, keyPath);

            var result = sut.ComputeSapisidHash("abc123", "https://voice.google.com");

            var payload = result["SAPISIDHASH ".Length..];
            var underscore = payload.IndexOf('_', StringComparison.Ordinal);
            var timestamp = payload[..underscore];
            var hash = payload[(underscore + 1)..];

            var input = $"{timestamp} abc123 https://voice.google.com";
#pragma warning disable CA5350 // SHA1 is required by the Google SAPISIDHASH spec
            var expected = Convert.ToHexStringLower(
                SHA1.HashData(Encoding.UTF8.GetBytes(input)));
#pragma warning restore CA5350

            hash.Should().Be(expected);
        }
        finally
        {
            File.Delete(cookiePath);
            File.Delete(keyPath);
        }
    }

    [Fact]
    public async Task GetValidCookiesAsync_LoadsFromEncryptedFile()
    {
        var cookiePath = Path.GetTempFileName();
        var keyPath = Path.GetTempFileName();
        try
        {
            var key = new byte[32];
            RandomNumberGenerator.Fill(key);
            var cookies = new GvCookieSet
            {
                Sapisid = "real-sapisid",
                Sid = "real-sid",
                Hsid = "real-hsid",
                Ssid = "real-ssid",
                Apisid = "real-apisid",
                Secure1Psid = "secure1",
                Secure3Psid = "secure3"
            };
            await File.WriteAllBytesAsync(keyPath, key);
            await File.WriteAllBytesAsync(cookiePath, TokenEncryption.Encrypt(cookies.Serialize(), key));

            var sut = new GvAuthService(cookiePath, keyPath);

            var result = await sut.GetValidCookiesAsync();

            result.Sapisid.Should().Be("real-sapisid");
            result.Sid.Should().Be("real-sid");
            result.Secure1Psid.Should().Be("secure1");
        }
        finally
        {
            File.Delete(cookiePath);
            File.Delete(keyPath);
        }
    }

    [Fact]
    public async Task GetValidCookiesAsync_CachesResult()
    {
        var cookiePath = Path.GetTempFileName();
        var keyPath = Path.GetTempFileName();
        try
        {
            var key = new byte[32];
            RandomNumberGenerator.Fill(key);
            var cookies = new GvCookieSet
            {
                Sapisid = "cached",
                Sid = "s",
                Hsid = "h",
                Ssid = "ss",
                Apisid = "a"
            };
            await File.WriteAllBytesAsync(keyPath, key);
            await File.WriteAllBytesAsync(cookiePath, TokenEncryption.Encrypt(cookies.Serialize(), key));

            var sut = new GvAuthService(cookiePath, keyPath);

            var first = await sut.GetValidCookiesAsync();
            var second = await sut.GetValidCookiesAsync();

            ReferenceEquals(first, second).Should().BeTrue("should return cached instance");
        }
        finally
        {
            File.Delete(cookiePath);
            File.Delete(keyPath);
        }
    }
}
