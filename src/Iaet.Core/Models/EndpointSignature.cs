using System.Text.RegularExpressions;

namespace Iaet.Core.Models;

public sealed partial record EndpointSignature
{
    public string Method { get; }
    public string NormalizedPath { get; }
    public string Normalized => $"{Method} {NormalizedPath}";

    private EndpointSignature(string method, string normalizedPath)
    {
        Method = method;
        NormalizedPath = normalizedPath;
    }

    public static EndpointSignature FromRequest(string method, string path)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(path);
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var normalized = string.Join("/",
            segments.Select(s => IdPattern().IsMatch(s) ? "{id}" : s));
        return new EndpointSignature(method.ToUpperInvariant(), "/" + normalized);
    }

    // Matches: pure digits, GUIDs, hex strings 8+ chars
    [GeneratedRegex(@"^(\d+|[0-9a-f]{8,}|[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})$", RegexOptions.IgnoreCase)]
    private static partial Regex IdPattern();
}
