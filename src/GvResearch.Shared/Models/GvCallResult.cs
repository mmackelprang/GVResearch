namespace GvResearch.Shared.Models;

public sealed record GvCallResult(string CallId, bool Success, string? ErrorMessage)
{
    public static GvCallResult Ok(string callId) => new(callId, Success: true, ErrorMessage: null);
    public static GvCallResult Fail(string errorMessage) => new(string.Empty, Success: false, errorMessage);
}
