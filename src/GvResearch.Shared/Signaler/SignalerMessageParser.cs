using System.Text.Json;

namespace GvResearch.Shared.Signaler;

public static class SignalerMessageParser
{
    public static IReadOnlyList<SignalerEvent> Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return [];

        var events = new List<SignalerEvent>();
        var now = DateTimeOffset.UtcNow;

        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Array)
                return [];

            foreach (var entry in root.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Array || entry.GetArrayLength() < 2)
                    continue;

                var eventArray = entry[1];
                if (eventArray.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var evt in eventArray.EnumerateArray())
                {
                    if (evt.ValueKind != JsonValueKind.Array || evt.GetArrayLength() < 1)
                        continue;

                    var eventType = evt[0].GetString();
                    var parsed = eventType switch
                    {
                        "sdp-offer" when evt.GetArrayLength() >= 3 =>
                            (SignalerEvent)new IncomingSdpOfferEvent(
                                evt[1].GetString() ?? string.Empty,
                                evt[2].GetString() ?? string.Empty,
                                now),

                        "sdp-answer" when evt.GetArrayLength() >= 3 =>
                            new SdpAnswerEvent(
                                evt[1].GetString() ?? string.Empty,
                                evt[2].GetString() ?? string.Empty,
                                now),

                        "hangup" when evt.GetArrayLength() >= 2 =>
                            new CallHangupEvent(
                                evt[1].GetString() ?? string.Empty,
                                now),

                        "ringing" when evt.GetArrayLength() >= 2 =>
                            new CallRingingEvent(
                                evt[1].GetString() ?? string.Empty,
                                now),

                        _ => new UnknownEvent(evt.ToString(), now),
                    };

                    events.Add(parsed);
                }
            }
        }
        catch (JsonException)
        {
            // Malformed response — return what we have so far
        }

        return events;
    }
}
