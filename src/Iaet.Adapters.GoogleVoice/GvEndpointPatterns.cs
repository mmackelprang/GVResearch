using System.Text.RegularExpressions;

namespace Iaet.Adapters.GoogleVoice;

public static partial class GvEndpointPatterns
{
    // TODO: Update these patterns based on Phase 1 capture findings
    public static readonly (Regex Pattern, string Category, string HumanName, bool IsDestructive)[] CallPatterns =
    [
        (CallInitiatePattern(), "calls", "Initiate Call", false),
        (CallStatusPattern(), "calls", "Get Call Status", false),
        (CallHangupPattern(), "calls", "Hangup Call", false),
        (CallHistoryPattern(), "calls", "Call History", false),
    ];

    [GeneratedRegex(@"/voice/call/initiate", RegexOptions.IgnoreCase)]
    private static partial Regex CallInitiatePattern();

    [GeneratedRegex(@"/voice/call/status", RegexOptions.IgnoreCase)]
    private static partial Regex CallStatusPattern();

    [GeneratedRegex(@"/voice/call/hangup", RegexOptions.IgnoreCase)]
    private static partial Regex CallHangupPattern();

    [GeneratedRegex(@"/voice/call/history", RegexOptions.IgnoreCase)]
    private static partial Regex CallHistoryPattern();
}
