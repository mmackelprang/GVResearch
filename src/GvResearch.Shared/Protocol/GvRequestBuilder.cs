using System.Text.Json;
using GvResearch.Shared.Models;

namespace GvResearch.Shared.Protocol;

public static class GvRequestBuilder
{
    private static int ThreadTypeToCode(GvThreadType? type) => type switch
    {
        GvThreadType.Calls => 1,
        GvThreadType.Sms => 3,
        GvThreadType.Voicemail => 4,
        GvThreadType.Missed => 1,
        _ => 2,
    };

    public static string BuildAccountGetRequest() => "[null,1]";

    public static string BuildThreadListRequest(GvThreadListOptions? options)
    {
        var type = ThreadTypeToCode(options?.Type);
        var pageSize = options?.MaxResults ?? 50;
        var cursor = options?.Cursor;

        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartArray();
        writer.WriteNumberValue(type);
        writer.WriteNumberValue(pageSize);
        writer.WriteNullValue();
        if (cursor is not null)
            writer.WriteStringValue(cursor);
        else
            writer.WriteNullValue();
        writer.WriteNullValue();
        writer.WriteStartArray();
        writer.WriteNullValue();
        writer.WriteNumberValue(1);
        writer.WriteNumberValue(1);
        writer.WriteNumberValue(1);
        writer.WriteEndArray();
        writer.WriteEndArray();
        writer.Flush();
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    public static string BuildThreadGetRequest(string threadId)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartArray();
        writer.WriteStringValue(threadId);
        writer.WriteNumberValue(50);
        writer.WriteNullValue();
        writer.WriteStartArray();
        writer.WriteNullValue();
        writer.WriteNumberValue(1);
        writer.WriteNumberValue(1);
        writer.WriteEndArray();
        writer.WriteEndArray();
        writer.Flush();
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    public static string BuildSendSmsRequest(string toNumber, string message)
    {
        var threadId = $"t.{toNumber}";
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartArray();
        writer.WriteNullValue();
        writer.WriteNullValue();
        writer.WriteNullValue();
        writer.WriteNullValue();
        writer.WriteStringValue(message);
        writer.WriteStringValue(threadId);
        writer.WriteNullValue();
        writer.WriteNullValue();
        writer.WriteStartArray(); writer.WriteEndArray();
        writer.WriteEndArray();
        writer.Flush();
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    public static string BuildSearchRequest(string query)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartArray();
        writer.WriteStringValue(query);
        writer.WriteNumberValue(50);
        writer.WriteEndArray();
        writer.Flush();
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    public static string BuildBatchUpdateRequest(IEnumerable<string> threadIds, string action)
    {
        ArgumentNullException.ThrowIfNull(threadIds);
        int attr1 = 0, attr2 = 0, attr3 = 0, attr4 = 0, attr5 = 0;
        int mask1 = 0, mask2 = 0, mask3 = 0, mask4 = 0, mask5 = 0;

        switch (action)
        {
            case "read":
                attr3 = 1; mask3 = 1;
                break;
            case "archive":
                attr5 = 1; mask5 = 1;
                break;
            case "spam":
                attr2 = 1; mask2 = 1;
                break;
        }

        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartArray();
        writer.WriteStartArray();
        foreach (var threadId in threadIds)
        {
            writer.WriteStartArray();
            writer.WriteStartArray();
            writer.WriteStringValue(threadId);
            writer.WriteNumberValue(attr1);
            writer.WriteNumberValue(attr2);
            writer.WriteNumberValue(attr3);
            writer.WriteNumberValue(attr4);
            writer.WriteNumberValue(attr5);
            writer.WriteEndArray();
            writer.WriteStartArray();
            writer.WriteNumberValue(mask1);
            writer.WriteNumberValue(mask2);
            writer.WriteNumberValue(mask3);
            writer.WriteNumberValue(mask4);
            writer.WriteNumberValue(mask5);
            writer.WriteEndArray();
            writer.WriteNumberValue(1);
            writer.WriteEndArray();
        }
        writer.WriteEndArray();
        writer.WriteEndArray();
        writer.Flush();
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    public static string BuildBatchDeleteRequest(IEnumerable<string> threadIds)
    {
        ArgumentNullException.ThrowIfNull(threadIds);
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartArray();
        writer.WriteStartArray();
        writer.WriteStartArray();
        foreach (var id in threadIds)
            writer.WriteStringValue(id);
        writer.WriteEndArray();
        writer.WriteEndArray();
        writer.WriteEndArray();
        writer.Flush();
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    public static string BuildMarkAllReadRequest() => "[]";

    public static string BuildThreadingInfoRequest() => "[]";

    public static string BuildSipRegisterInfoRequest() => "[3,null]";
}
