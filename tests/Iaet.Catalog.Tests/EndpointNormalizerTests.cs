using FluentAssertions;
using Iaet.Catalog;

namespace Iaet.Catalog.Tests;

public class EndpointNormalizerTests
{
    [Theory]
    [InlineData("https://voice.google.com/api/v1/users/12345/calls", "GET", "GET /api/v1/users/{id}/calls")]
    [InlineData("https://voice.google.com/api/messages", "POST", "POST /api/messages")]
    [InlineData("https://example.com/rpc/list?key=abc123", "POST", "POST /rpc/list")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1054:URI-like parameters should not be strings")]
    public void NormalizeUrl_ExtractsPathAndNormalizesIds(string url, string method, string expected)
    {
        var result = EndpointNormalizer.Normalize(method, url);
        result.Should().Be(expected);
    }
}
