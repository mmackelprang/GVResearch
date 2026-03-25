using Iaet.Core.Models;

namespace Iaet.Catalog;

public static class EndpointNormalizer
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1054:URI-like parameters should not be strings")]
    public static string Normalize(string method, string fullUrl)
    {
        var uri = new Uri(fullUrl);
        var sig = EndpointSignature.FromRequest(method, uri.AbsolutePath);
        return sig.Normalized;
    }
}
