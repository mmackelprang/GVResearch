using System.Text.Json;
using GvResearch.Shared.Models;

namespace GvResearch.Shared.Protocol;

public static class GvProtobufJsonParser
{
    internal static GvMessageType MapMessageType(int code) => code switch
    {
        1 => GvMessageType.RecordedCall,
        2 => GvMessageType.Voicemail,
        10 => GvMessageType.Sms,
        11 => GvMessageType.Sms,
        14 => GvMessageType.RecordedCall,
        _ => GvMessageType.MissedCall,
    };

    public static GvAccount ParseAccount(JsonElement root)
    {
        // root[0] can be a string (phone number) or an array (nested config).
        // The real GV response wraps the account in an outer array.
        var firstEl = root[0];
        var phoneNumber = firstEl.ValueKind == JsonValueKind.String
            ? firstEl.GetString() ?? string.Empty
            : (firstEl.ValueKind == JsonValueKind.Array && firstEl.GetArrayLength() > 0
                && firstEl[0].ValueKind == JsonValueKind.String
                    ? firstEl[0].GetString() ?? string.Empty
                    : string.Empty);
        var phones = new List<GvPhoneNumber>
        {
            new(phoneNumber, PhoneNumberType.GoogleVoice, IsPrimary: true)
        };

        var devices = new List<GvDevice>();
        if (root.GetArrayLength() > 5 && root[5].ValueKind == JsonValueKind.Array)
        {
            foreach (var dev in root[5].EnumerateArray())
            {
                var id = dev.GetArrayLength() > 0 ? dev[0].GetString() ?? "" : "";
                var name = dev.GetArrayLength() > 1 ? dev[1].GetString() ?? "" : "";
                devices.Add(new GvDevice(id, name, DeviceType.Unknown));
            }
        }

        var settings = new GvSettings(DoNotDisturb: false, VoicemailGreetingUrl: null);
        return new GvAccount(phones, devices, settings);
    }

    public static GvThreadPage ParseThreadList(JsonElement root)
    {
        var threads = new List<GvThread>();
        string? nextCursor = null;

        // GV thread list: root[0] is an array of thread elements.
        // Each thread element is [threadId, isRead, [messages...]].
        if (root.GetArrayLength() > 0 && root[0].ValueKind == JsonValueKind.Array)
        {
            foreach (var threadEl in root[0].EnumerateArray())
            {
                if (threadEl.ValueKind == JsonValueKind.Array && threadEl.GetArrayLength() > 0)
                    threads.Add(ParseThread(threadEl));
            }
        }

        if (root.GetArrayLength() > 1 && root[1].ValueKind == JsonValueKind.String)
        {
            nextCursor = root[1].GetString();
        }

        return new GvThreadPage(threads, nextCursor, threads.Count);
    }

    public static GvThread ParseThread(JsonElement threadEl)
    {
        var id = threadEl[0].GetString() ?? string.Empty;
        var isRead = threadEl[1].ValueKind == JsonValueKind.Number && threadEl[1].GetInt32() == 1;

        var messages = new List<GvMessage>();
        var participants = new HashSet<string>(StringComparer.Ordinal);

        if (threadEl.GetArrayLength() > 2 && threadEl[2].ValueKind == JsonValueKind.Array)
        {
            foreach (var msgEl in threadEl[2].EnumerateArray())
            {
                var msg = ParseMessage(msgEl);
                messages.Add(msg);
                if (msg.SenderNumber is not null)
                    participants.Add(msg.SenderNumber);
            }
        }

        var threadType = InferThreadType(id, messages);
        var timestamp = messages.Count > 0
            ? messages[^1].Timestamp
            : DateTimeOffset.MinValue;

        return new GvThread(id, threadType, messages, participants.ToList(), timestamp, isRead);
    }

    public static GvUnreadCounts ParseUnreadCounts(JsonElement root)
    {
        int sms = 0, voicemail = 0, missed = 0;

        // GV unread counts: root[0][0] is [[type,null,count],...].
        if (root.GetArrayLength() > 0
            && root[0].ValueKind == JsonValueKind.Array
            && root[0].GetArrayLength() > 0
            && root[0][0].ValueKind == JsonValueKind.Array)
        {
            var countsArray = root[0][0];
            foreach (var entry in countsArray.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Array || entry.GetArrayLength() < 3)
                    continue;
                var type = entry[0].GetInt32();
                var count = entry[2].GetInt32();
                switch (type)
                {
                    case 1: missed = count; break;
                    case 3: sms = count; break;
                    case 4: voicemail = count; break;
                }
            }
        }

        return new GvUnreadCounts(sms, voicemail, missed, sms + voicemail + missed);
    }

    public static GvSmsResult ParseSendSms(JsonElement root)
    {
        var threadId = root.GetArrayLength() > 1
            ? root[1].GetString() ?? string.Empty
            : string.Empty;

        return new GvSmsResult(threadId, Success: !string.IsNullOrEmpty(threadId));
    }

    private static GvMessage ParseMessage(JsonElement msgEl)
    {
        var id = msgEl[0].GetString() ?? string.Empty;

        var timestampMs = msgEl.GetArrayLength() > 1 && msgEl[1].ValueKind == JsonValueKind.Number
            ? msgEl[1].GetInt64()
            : 0;
        var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(timestampMs);

        string? senderNumber = null;
        if (msgEl.GetArrayLength() > 3 && msgEl[3].ValueKind == JsonValueKind.Array && msgEl[3].GetArrayLength() > 0)
        {
            senderNumber = msgEl[3][0].GetString();
        }

        var typeCode = msgEl.GetArrayLength() > 4 && msgEl[4].ValueKind == JsonValueKind.Number
            ? msgEl[4].GetInt32()
            : 0;

        string? text = null;
        if (msgEl.GetArrayLength() > 11 && msgEl[11].ValueKind == JsonValueKind.String)
        {
            text = msgEl[11].GetString();
        }

        return new GvMessage(id, text, senderNumber, timestamp, MapMessageType(typeCode));
    }

    private static GvThreadType InferThreadType(string threadId, List<GvMessage> messages)
    {
        if (threadId.StartsWith("t.", StringComparison.Ordinal))
            return GvThreadType.Sms;
        if (threadId.StartsWith("c.", StringComparison.Ordinal))
            return GvThreadType.Calls;
        if (messages.Count > 0 && messages[0].Type == GvMessageType.Voicemail)
            return GvThreadType.Voicemail;
        return GvThreadType.All;
    }
}
