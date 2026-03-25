namespace GvResearch.Shared.Authentication;

/// <summary>
/// Reads an AES-256-encrypted token file and caches the result in memory.
/// Uses a <see cref="SemaphoreSlim"/> to ensure the file is only read once
/// under concurrent access.
/// </summary>
public sealed class EncryptedFileTokenService : IGvTokenService
{
    private readonly string _tokenPath;
    private readonly string _keyPath;
    private readonly Action? _onFileRead;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private string? _cachedToken;
    private bool _disposed;

    /// <inheritdoc />
    public event EventHandler<TokenExpiredEventArgs>? TokenExpired;

    /// <param name="tokenPath">Path to the AES-256-encrypted token file.</param>
    /// <param name="keyPath">Path to the 32-byte raw key file.</param>
    /// <param name="onFileRead">Optional callback invoked each time the file is actually read (used in tests).</param>
    public EncryptedFileTokenService(string tokenPath, string keyPath, Action? onFileRead = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyPath);

        _tokenPath = tokenPath;
        _keyPath = keyPath;
        _onFileRead = onFileRead;
    }

    /// <inheritdoc />
    public async Task<string> GetValidTokenAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Acquire the lock unconditionally; the first caller populates the cache,
        // subsequent callers return immediately once the semaphore is released.
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _cachedToken ??= await ReadTokenFromFileAsync(cancellationToken).ConfigureAwait(false);
            return _cachedToken;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task RefreshTokenAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _cachedToken = null;

            try
            {
                _cachedToken = await ReadTokenFromFileAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (IOException ex)
            {
                OnTokenExpired(ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                OnTokenExpired(ex);
            }
            catch (System.Security.Cryptography.CryptographicException ex)
            {
                OnTokenExpired(ex);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<string> ReadTokenFromFileAsync(CancellationToken cancellationToken)
    {
        _onFileRead?.Invoke();

        var key = await File.ReadAllBytesAsync(_keyPath, cancellationToken).ConfigureAwait(false);
        var ciphertext = await File.ReadAllBytesAsync(_tokenPath, cancellationToken).ConfigureAwait(false);
        return TokenEncryption.Decrypt(ciphertext, key);
    }

    private void OnTokenExpired(Exception cause)
    {
        TokenExpired?.Invoke(this, new TokenExpiredEventArgs { Cause = cause });
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cachedToken = null;
        _lock.Dispose();
    }
}
