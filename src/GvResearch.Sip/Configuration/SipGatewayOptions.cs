namespace GvResearch.Sip.Configuration;

/// <summary>
/// Top-level configuration for the SIP gateway.
/// </summary>
public sealed class SipGatewayOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "SipGateway";

    /// <summary>UDP port on which the SIP transport listens.</summary>
    public int SipPort { get; set; } = 5060;

    /// <summary>SIP domain used as the realm for digest authentication.</summary>
    public string SipDomain { get; set; } = "gvresearch.local";

    /// <summary>Pre-configured SIP accounts accepted by the registrar.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only",
        Justification = "Options pattern requires a settable collection for model binding.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002:Do not expose generic lists",
        Justification = "Options pattern requires a concrete List<T> for model binding.")]
    public List<SipAccountOptions> Accounts { get; set; } = [];
}

/// <summary>
/// Credentials and display name for a single SIP account.
/// </summary>
public sealed class SipAccountOptions
{
    /// <summary>SIP username (used as the authentication realm identity).</summary>
    public required string Username { get; set; }

    /// <summary>Plain-text password used to generate digest HA1.</summary>
    public required string Password { get; set; }

    /// <summary>Optional human-readable display name.</summary>
    public string? DisplayName { get; set; }
}
