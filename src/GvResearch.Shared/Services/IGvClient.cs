using GvResearch.Shared.Models;

namespace GvResearch.Shared.Services;

public interface IGvClient : IAsyncDisposable
{
    IGvAccountClient Account { get; }
    IGvThreadClient Threads { get; }
    IGvSmsClient Sms { get; }
    IGvCallClient Calls { get; }
}

public interface IGvAccountClient
{
    Task<GvAccount> GetAsync(CancellationToken ct = default);
}

public interface IGvThreadClient
{
    Task<GvThreadPage> ListAsync(GvThreadListOptions? options = null, CancellationToken ct = default);
    Task<GvThread> GetAsync(string threadId, CancellationToken ct = default);
    Task MarkReadAsync(IEnumerable<string> threadIds, CancellationToken ct = default);
    Task ArchiveAsync(IEnumerable<string> threadIds, CancellationToken ct = default);
    Task MarkSpamAsync(IEnumerable<string> threadIds, CancellationToken ct = default);
    Task DeleteAsync(IEnumerable<string> threadIds, CancellationToken ct = default);
    Task MarkAllReadAsync(CancellationToken ct = default);
    Task<GvUnreadCounts> GetUnreadCountsAsync(CancellationToken ct = default);
    Task<GvThreadPage> SearchAsync(string query, CancellationToken ct = default);
}

public interface IGvSmsClient
{
    Task<GvSmsResult> SendAsync(string toNumber, string message, CancellationToken ct = default);
}

public interface IGvCallClient
{
    Task<GvCallResult> InitiateAsync(string toNumber, CancellationToken ct = default);
    Task<GvCallStatus> GetStatusAsync(string callId, CancellationToken ct = default);
    Task HangupAsync(string callId, CancellationToken ct = default);
}
