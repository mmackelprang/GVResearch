using System.Text.Json;
using FluentAssertions;
using GvResearch.Shared.Models;
using GvResearch.Shared.Protocol;

namespace GvResearch.Shared.Tests.Protocol;

public sealed class GvProtobufJsonParserTests
{
    [Fact]
    public void ParseAccount_ExtractsPrimaryPhoneNumber()
    {
        var json = """["+19196706660",null,[],null,null,[]]""";
        var root = JsonDocument.Parse(json).RootElement;

        var account = GvProtobufJsonParser.ParseAccount(root);

        account.PhoneNumbers.Should().ContainSingle();
        account.PhoneNumbers[0].Number.Should().Be("+19196706660");
        account.PhoneNumbers[0].IsPrimary.Should().BeTrue();
    }

    [Fact]
    public void ParseThreadList_ExtractsThreads()
    {
        var json = """
            [[[
                "t.+15551234567",
                1,
                [["msg1",1234567890000,"+15550000001",["+15551234567"],10,1,null,null,null,null,null,"Hello!",null,null,0,1]]
            ]]]
            """;
        var root = JsonDocument.Parse(json).RootElement;

        var page = GvProtobufJsonParser.ParseThreadList(root);

        page.Threads.Should().ContainSingle();
        page.Threads[0].Id.Should().Be("t.+15551234567");
        page.Threads[0].IsRead.Should().BeTrue();
        page.Threads[0].Messages.Should().ContainSingle();
        page.Threads[0].Messages[0].Text.Should().Be("Hello!");
    }

    [Fact]
    public void ParseThread_ExtractsMessages()
    {
        var json = """
            [
                "t.+15551234567",
                0,
                [
                    ["msg1",1234567890000,"+15550000001",["+15551234567"],10,0,null,null,null,null,null,"First message",null,null,0,0],
                    ["msg2",1234567891000,"+15551234567",["+15550000001"],11,1,null,null,null,null,null,"Reply",null,null,0,1]
                ]
            ]
            """;
        var root = JsonDocument.Parse(json).RootElement;

        var thread = GvProtobufJsonParser.ParseThread(root);

        thread.Id.Should().Be("t.+15551234567");
        thread.IsRead.Should().BeFalse();
        thread.Messages.Should().HaveCount(2);
        thread.Messages[0].Text.Should().Be("First message");
        thread.Messages[0].Type.Should().Be(GvMessageType.Sms);
        thread.Messages[1].Text.Should().Be("Reply");
    }

    [Fact]
    public void ParseUnreadCounts_ExtractsCounts()
    {
        var json = """[[[[1,null,5],[4,null,2],[3,null,10],[5,null,0],[6,null,1]]]]""";
        var root = JsonDocument.Parse(json).RootElement;

        var counts = GvProtobufJsonParser.ParseUnreadCounts(root);

        counts.Missed.Should().Be(5);
        counts.Voicemail.Should().Be(2);
        counts.Sms.Should().Be(10);
    }

    [Fact]
    public void ParseSendSms_ExtractsResult()
    {
        var json = """[null,"t.+15551234567","msg-hash-123",1234567890000,[1,2]]""";
        var root = JsonDocument.Parse(json).RootElement;

        var result = GvProtobufJsonParser.ParseSendSms(root);

        result.ThreadId.Should().Be("t.+15551234567");
        result.Success.Should().BeTrue();
    }

    [Theory]
    [InlineData(10, GvMessageType.Sms)]
    [InlineData(11, GvMessageType.Sms)]
    [InlineData(2, GvMessageType.Voicemail)]
    [InlineData(1, GvMessageType.RecordedCall)]
    [InlineData(14, GvMessageType.RecordedCall)]
    public void ParseThread_MapsMessageTypeCorrectly(int typeCode, GvMessageType expected)
    {
        var json = $$$"""["t.+1",0,[["m1",1000,"+1",["+2"],{{{typeCode}}},0,null,null,null,null,null,"text",null,null,0,0]]]""";
        var root = JsonDocument.Parse(json).RootElement;

        var thread = GvProtobufJsonParser.ParseThread(root);

        thread.Messages[0].Type.Should().Be(expected);
    }
}
