namespace GvResearch.Api.Models;

/// <summary>Strongly-typed identifier for a call record.</summary>
public readonly record struct CallId(Guid Value);

/// <summary>Strongly-typed phone number.</summary>
public readonly record struct PhoneNumber(string Value);

/// <summary>Direction of a call relative to the Google Voice number.</summary>
public enum CallDirection
{
    /// <summary>Call came in to the GV number.</summary>
    Inbound,

    /// <summary>Call was placed from the GV number.</summary>
    Outbound,
}

/// <summary>Lifecycle status of a call.</summary>
public enum CallStatus
{
    /// <summary>Call is ringing.</summary>
    Ringing,

    /// <summary>Call is connected and active.</summary>
    Active,

    /// <summary>Call ended normally.</summary>
    Completed,

    /// <summary>Call was not answered.</summary>
    Missed,

    /// <summary>Caller left a voicemail.</summary>
    Voicemail,

    /// <summary>Call failed to connect.</summary>
    Failed,
}

/// <summary>A single call record.</summary>
public sealed record CallRecord(
    CallId Id,
    CallDirection Direction,
    PhoneNumber FromNumber,
    PhoneNumber ToNumber,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    int? DurationSeconds,
    CallStatus Status);

/// <summary>Request body for initiating an outbound call.</summary>
public sealed record InitiateCallRequest(string FromNumber, string ToNumber);
