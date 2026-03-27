namespace GvResearch.Shared.Exceptions;

public class GvRateLimitException : GvApiException
{
    public string Endpoint { get; }

    public GvRateLimitException(string endpoint)
        : base($"Rate limit exceeded for endpoint: {endpoint}", statusCode: 429)
    {
        Endpoint = endpoint;
    }
}
