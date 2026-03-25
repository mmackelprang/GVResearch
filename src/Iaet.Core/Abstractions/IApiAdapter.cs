using Iaet.Core.Models;

namespace Iaet.Core.Abstractions;

public interface IApiAdapter
{
    string TargetName { get; }
    bool CanHandle(CapturedRequest request);
    EndpointDescriptor Describe(CapturedRequest request);
}

public sealed record EndpointDescriptor(
    string HumanName,
    string Category,
    bool IsDestructive,
    string? Notes
);
