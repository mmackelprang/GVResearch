using Iaet.Core.Models;

namespace Iaet.Core.Abstractions;

public interface IEndpointCatalog
{
    Task SaveSessionAsync(CaptureSessionInfo session, CancellationToken ct = default);
    Task SaveRequestAsync(CapturedRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<CaptureSessionInfo>> ListSessionsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CapturedRequest>> GetRequestsBySessionAsync(Guid sessionId, CancellationToken ct = default);
    Task<IReadOnlyList<EndpointGroup>> GetEndpointGroupsAsync(Guid sessionId, CancellationToken ct = default);
}
