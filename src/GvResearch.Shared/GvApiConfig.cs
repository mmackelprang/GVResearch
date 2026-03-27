namespace GvResearch.Shared;

/// <summary>
/// Holds runtime configuration for GV API requests.
/// Injected as a singleton; all service clients read from this.
/// </summary>
public sealed class GvApiConfig
{
    /// <summary>
    /// The GV API key (public, browser-embedded, scoped to Voice).
    /// Read from configuration — never hardcode in source.
    /// </summary>
    public required string ApiKey { get; init; }

    /// <summary>
    /// Builds the query string suffix for GV API requests.
    /// </summary>
    public string QueryString => string.IsNullOrEmpty(ApiKey)
        ? "alt=protojson"
        : $"alt=protojson&key={Uri.EscapeDataString(ApiKey)}";
}
