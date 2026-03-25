using Iaet.Core.Abstractions;
using Iaet.Core.Models;

namespace Iaet.Adapters.GoogleVoice;

public sealed class GoogleVoiceAdapter : IApiAdapter
{
    public string TargetName => "Google Voice";

    public bool CanHandle(CapturedRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.Url.Contains("voice.google.com", StringComparison.OrdinalIgnoreCase)
            || request.Url.Contains("clients6.google.com", StringComparison.OrdinalIgnoreCase);
    }

    public EndpointDescriptor Describe(CapturedRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        foreach (var (pattern, category, name, isDestructive) in GvEndpointPatterns.CallPatterns)
        {
            if (pattern.IsMatch(request.Url))
            {
                return new EndpointDescriptor(name, category, isDestructive, Notes: null);
            }
        }

        return new EndpointDescriptor(
            HumanName: "Unknown GV Endpoint",
            Category: "unknown",
            IsDestructive: false,
            Notes: $"URL: {request.Url}"
        );
    }
}
