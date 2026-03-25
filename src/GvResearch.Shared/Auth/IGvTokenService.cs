namespace GvResearch.Shared.Authentication;

/// <summary>
/// Event args raised when a token has expired or cannot be refreshed.
/// </summary>
public sealed class TokenExpiredEventArgs : EventArgs
{
    /// <summary>Gets the exception that caused the expiry, if any.</summary>
    public Exception? Cause { get; init; }
}

/// <summary>
/// Provides access to a valid Google Voice authentication token.
/// </summary>
public interface IGvTokenService : IDisposable
{
    /// <summary>Fired when the token has expired and cannot be refreshed automatically.</summary>
    event EventHandler<TokenExpiredEventArgs> TokenExpired;

    /// <summary>Returns a valid token, reading from cache when available.</summary>
    Task<string> GetValidTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>Clears the in-memory cache and re-reads the token from the backing store.</summary>
    Task RefreshTokenAsync(CancellationToken cancellationToken = default);
}
