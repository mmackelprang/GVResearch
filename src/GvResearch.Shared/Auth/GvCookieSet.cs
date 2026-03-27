using System.Text;
using System.Text.Json;

namespace GvResearch.Shared.Auth;

public sealed class GvCookieSet
{
    public required string Sapisid { get; init; }
    public required string Sid { get; init; }
    public required string Hsid { get; init; }
    public required string Ssid { get; init; }
    public required string Apisid { get; init; }
    public string? Secure1Psid { get; init; }
    public string? Secure3Psid { get; init; }

    public string ToCookieHeader()
    {
        var sb = new StringBuilder();
        sb.Append("SAPISID=").Append(Sapisid)
          .Append("; SID=").Append(Sid)
          .Append("; HSID=").Append(Hsid)
          .Append("; SSID=").Append(Ssid)
          .Append("; APISID=").Append(Apisid);
        if (Secure1Psid is not null)
            sb.Append("; __Secure-1PSID=").Append(Secure1Psid);
        if (Secure3Psid is not null)
            sb.Append("; __Secure-3PSID=").Append(Secure3Psid);
        return sb.ToString();
    }

    public string Serialize() => JsonSerializer.Serialize(this);

    public static GvCookieSet Deserialize(string json) =>
        JsonSerializer.Deserialize<GvCookieSet>(json)
        ?? throw new InvalidOperationException("Failed to deserialize cookie set.");
}
