namespace GvResearch.Shared.Exceptions;

public class GvApiException : Exception
{
    public int? StatusCode { get; }

    public GvApiException()
        : base()
    {
    }

    public GvApiException(string message)
        : base(message)
    {
    }

    public GvApiException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public GvApiException(string message, int? statusCode = null)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public GvApiException(string message, int? statusCode, Exception innerException)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }
}
