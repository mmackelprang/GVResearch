using Iaet.Core.Models;

namespace Iaet.Core.Abstractions;

public interface ICaptureSession : IAsyncDisposable
{
    Guid SessionId { get; }
    string TargetApplication { get; }
    bool IsRecording { get; }
    Task StartAsync(Uri url, CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
    IAsyncEnumerable<CapturedRequest> GetCapturedRequestsAsync(CancellationToken ct = default);
}
