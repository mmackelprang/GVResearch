using System.Text.Json;
using FluentAssertions;
using GvResearch.Shared.Models;
using GvResearch.Shared.Protocol;

namespace GvResearch.Shared.Tests.Protocol;

public sealed class GvRequestBuilderTests
{
    [Fact]
    public void BuildAccountGetRequest_ReturnsCorrectJson()
    {
        var result = GvRequestBuilder.BuildAccountGetRequest();
        result.Should().Be("[null,1]");
    }

    [Fact]
    public void BuildThreadListRequest_WithDefaults_ReturnsCorrectJson()
    {
        var result = GvRequestBuilder.BuildThreadListRequest(null);
        var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;
        root[0].GetInt32().Should().Be(2);
        root[1].GetInt32().Should().Be(50);
        root[2].GetInt32().Should().Be(15, "flags field observed in captures");
    }

    [Fact]
    public void BuildThreadListRequest_WithSmsType_UsesTypeCode3()
    {
        var options = new GvThreadListOptions(Type: GvThreadType.Sms, MaxResults: 20);
        var result = GvRequestBuilder.BuildThreadListRequest(options);
        var doc = JsonDocument.Parse(result);
        doc.RootElement[0].GetInt32().Should().Be(3);
        doc.RootElement[1].GetInt32().Should().Be(20);
    }

    [Fact]
    public void BuildThreadListRequest_WithCursor_IncludesCursor()
    {
        var options = new GvThreadListOptions(Cursor: "next-page-token");
        var result = GvRequestBuilder.BuildThreadListRequest(options);
        result.Should().Contain("next-page-token");
    }

    [Fact]
    public void BuildThreadGetRequest_ReturnsCorrectJson()
    {
        var result = GvRequestBuilder.BuildThreadGetRequest("t.+15551234567");
        var doc = JsonDocument.Parse(result);
        doc.RootElement[0].GetString().Should().Be("t.+15551234567");
    }

    [Fact]
    public void BuildSendSmsRequest_ReturnsCorrectJson()
    {
        var result = GvRequestBuilder.BuildSendSmsRequest("+15551234567", "Hello!");
        var doc = JsonDocument.Parse(result);
        doc.RootElement[4].GetString().Should().Be("Hello!");
        doc.RootElement[5].GetString().Should().Be("t.+15551234567");
    }

    [Fact]
    public void BuildSearchRequest_ReturnsCorrectJson()
    {
        var result = GvRequestBuilder.BuildSearchRequest("hello world");
        var doc = JsonDocument.Parse(result);
        doc.RootElement[0].GetString().Should().Be("hello world");
    }

    [Fact]
    public void BuildBatchDeleteRequest_ReturnsCorrectJson()
    {
        var result = GvRequestBuilder.BuildBatchDeleteRequest(["t.+1111", "t.+2222"]);
        var doc = JsonDocument.Parse(result);
        doc.RootElement[0][0][0].GetString().Should().Be("t.+1111");
        doc.RootElement[0][0][1].GetString().Should().Be("t.+2222");
    }

    [Fact]
    public void BuildMarkAllReadRequest_ReturnsEmptyArray()
    {
        var result = GvRequestBuilder.BuildMarkAllReadRequest();
        result.Should().Be("[]");
    }

    [Fact]
    public void BuildThreadingInfoRequest_ReturnsEmptyArray()
    {
        var result = GvRequestBuilder.BuildThreadingInfoRequest();
        result.Should().Be("[]");
    }
}
