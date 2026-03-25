using FluentAssertions;
using Iaet.Core.Models;

namespace Iaet.Core.Tests.Models;

public class EndpointSignatureTests
{
    [Theory]
    [InlineData("GET", "/api/users/123/posts/456", "GET /api/users/{id}/posts/{id}")]
    [InlineData("POST", "/api/v1/messages", "POST /api/v1/messages")]
    [InlineData("GET", "/api/items/a8f3b2c1d", "GET /api/items/{id}")]
    public void Normalize_ReplacesIdSegments(string method, string path, string expected)
    {
        var sig = EndpointSignature.FromRequest(method, path);
        sig.Normalized.Should().Be(expected);
    }

    [Fact]
    public void Equals_SameNormalized_AreEqual()
    {
        var a = EndpointSignature.FromRequest("GET", "/api/users/123");
        var b = EndpointSignature.FromRequest("GET", "/api/users/456");
        a.Should().Be(b);
    }

    [Fact]
    public void Equals_DifferentMethod_AreNotEqual()
    {
        var a = EndpointSignature.FromRequest("GET", "/api/users/123");
        var b = EndpointSignature.FromRequest("POST", "/api/users/123");
        a.Should().NotBe(b);
    }
}
