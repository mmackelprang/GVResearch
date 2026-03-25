using Iaet.Core.Models;

namespace Iaet.Core.Abstractions;

public interface ICaptureSession : IAsyncDisposable
{
    Guid SessionId { get; }
    string TargetApplication { get; }
    bool IsRecording { get; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1054:URI-like parameters should not be strings")]
    Task StartAsync(string url, CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
    IAsyncEnumerable<CapturedRequest> GetCapturedRequestsAsync(CancellationToken ct = default);
}
