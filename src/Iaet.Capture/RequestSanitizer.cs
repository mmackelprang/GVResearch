namespace Iaet.Capture;

public static class RequestSanitizer
{
    private static readonly HashSet<string> SensitiveHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "authorization", "cookie", "set-cookie", "x-goog-authuser",
        "x-csrf-token", "x-xsrf-token"
    };

    public static Dictionary<string, string> SanitizeHeaders(IDictionary<string, string> headers)
    {
        return headers.ToDictionary(
            kvp => kvp.Key,
            kvp => SensitiveHeaders.Contains(kvp.Key) ? "<REDACTED>" : kvp.Value,
            StringComparer.OrdinalIgnoreCase
        );
    }
}
