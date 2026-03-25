using System.Collections.Concurrent;

namespace GvResearch.Sip.Registrar;

/// <summary>
/// A single SIP registration record stored by the registrar.
/// </summary>
/// <param name="Username">Authenticated SIP username.</param>
/// <param name="ContactUri">The contact URI supplied by the UA.</param>
/// <param name="ExpiresAt">UTC time at which the registration expires.</param>
public sealed record SipRegistration(
    string Username,
    Uri ContactUri,
    DateTimeOffset ExpiresAt);

/// <summary>
/// Thread-safe in-memory store for SIP registrations.
/// Keyed on username; superseded registrations are replaced on re-registration.
/// </summary>
public sealed class RegistrationStore
{
    private readonly ConcurrentDictionary<string, SipRegistration> _registrations =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Adds or replaces a registration for <paramref name="username"/>.
    /// </summary>
    public void AddOrUpdate(string username, Uri contactUri, DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentNullException.ThrowIfNull(contactUri);

        var registration = new SipRegistration(username, contactUri, expiresAt);
        _registrations[username] = registration;
    }

    /// <summary>
    /// Returns all registrations that have not yet expired.
    /// </summary>
    public IReadOnlyList<SipRegistration> GetAll()
    {
        var now = DateTimeOffset.UtcNow;
        return _registrations.Values
            .Where(r => r.ExpiresAt > now)
            .ToList();
    }

    /// <summary>
    /// Removes the registration for <paramref name="username"/>, if present.
    /// </summary>
    /// <returns><see langword="true"/> if a registration was removed.</returns>
    public bool Remove(string username)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        return _registrations.TryRemove(username, out _);
    }

    /// <summary>
    /// Returns the registration for <paramref name="username"/>, or <see langword="null"/>
    /// if not found or expired.
    /// </summary>
    public SipRegistration? TryGet(string username)
    {
        if (_registrations.TryGetValue(username, out var reg) &&
            reg.ExpiresAt > DateTimeOffset.UtcNow)
        {
            return reg;
        }

        return null;
    }
}
