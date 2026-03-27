namespace GvResearch.Shared.Exceptions;

public class GvRateLimitException : GvApiException
{
    public string Endpoint { get; }

    public GvRateLimitException()
        : base()
    {
        Endpoint = string.Empty;
    }

    public GvRateLimitException(string endpoint)
        : base($"Rate limit exceeded for endpoint: {endpoint}", statusCode: 429)
    {
        Endpoint = endpoint;
    }

    public GvRateLimitException(string message, Exception innerException)
        : base(message, innerException)
    {
        Endpoint = string.Empty;
    }
}
