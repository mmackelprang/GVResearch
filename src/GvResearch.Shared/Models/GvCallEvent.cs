namespace GvResearch.Shared.Models;

/// <summary>Types of real-time events that can occur during a Google Voice call.</summary>
public enum GvCallEventType
{
    /// <summary>Event type is unrecognised.</summary>
    Unknown,

    /// <summary>Call has started ringing.</summary>
    Ringing,

    /// <summary>Call was answered.</summary>
    Answered,

    /// <summary>Call was hung up.</summary>
    HungUp,

    /// <summary>Call failed.</summary>
    Failed,
}

/// <summary>
/// A real-time event received for a Google Voice call.
/// </summary>
/// <param name="GvCallId">The GV-assigned call identifier.</param>
/// <param name="EventType">The type of event.</param>
/// <param name="OccurredAt">When the event occurred.</param>
/// <param name="Payload">Optional raw payload from GV for diagnostics.</param>
public sealed record GvCallEvent(
    string GvCallId,
    GvCallEventType EventType,
    DateTimeOffset OccurredAt,
    string? Payload = null);
