using FluentAssertions;
using GvResearch.Shared.Authentication;

namespace GvResearch.Shared.Tests.Auth;

public sealed class EncryptedFileTokenServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _tokenPath;
    private readonly string _keyPath;
    private readonly byte[] _key;

    public EncryptedFileTokenServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"GvTokenTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _tokenPath = Path.Combine(_tempDir, "token.enc");
        _keyPath = Path.Combine(_tempDir, "key.bin");

        _key = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(_key);
        File.WriteAllBytes(_keyPath, _key);
    }

    private void WriteEncryptedToken(string token)
    {
        var ciphertext = TokenEncryption.Encrypt(token, _key);
        File.WriteAllBytes(_tokenPath, ciphertext);
    }

    [Fact]
    public async Task GetValidTokenAsync_ReadsAndDecryptsTokenFromFile()
    {
        WriteEncryptedToken("gv-auth-token-abc123");
        using var svc = new EncryptedFileTokenService(_tokenPath, _keyPath);

        var token = await svc.GetValidTokenAsync();

        token.Should().Be("gv-auth-token-abc123");
    }

    [Fact]
    public async Task GetValidTokenAsync_CalledTwice_ReturnsCachedValue()
    {
        WriteEncryptedToken("cached-token");
        using var svc = new EncryptedFileTokenService(_tokenPath, _keyPath);

        var first = await svc.GetValidTokenAsync();
        // Remove the file to prove second call uses cache
        File.Delete(_tokenPath);
        var second = await svc.GetValidTokenAsync();

        second.Should().Be(first);
    }

    [Fact]
    public async Task RefreshTokenAsync_ClearsCacheAndRereadsFile()
    {
        WriteEncryptedToken("original-token");
        using var svc = new EncryptedFileTokenService(_tokenPath, _keyPath);

        var first = await svc.GetValidTokenAsync();
        first.Should().Be("original-token");

        WriteEncryptedToken("refreshed-token");
        await svc.RefreshTokenAsync();

        var second = await svc.GetValidTokenAsync();
        second.Should().Be("refreshed-token");
    }

    [Fact]
    public async Task RefreshTokenAsync_WhenFileMissing_FiresTokenExpiredEvent()
    {
        WriteEncryptedToken("token");
        using var svc = new EncryptedFileTokenService(_tokenPath, _keyPath);
        await svc.GetValidTokenAsync();

        var eventFired = false;
        svc.TokenExpired += (_, _) => eventFired = true;

        File.Delete(_tokenPath);
        await svc.RefreshTokenAsync();

        eventFired.Should().BeTrue();
    }

    [Fact]
    public async Task GetValidTokenAsync_ConcurrentCalls_OnlyReadsFileOnce()
    {
        WriteEncryptedToken("concurrent-token");
        var readCount = 0;
        using var svc = new EncryptedFileTokenService(_tokenPath, _keyPath, () => readCount++);

        var tasks = Enumerable.Range(0, 10).Select(_ => svc.GetValidTokenAsync());
        var results = await Task.WhenAll(tasks);

        readCount.Should().Be(1);
        results.Should().AllBe("concurrent-token");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
