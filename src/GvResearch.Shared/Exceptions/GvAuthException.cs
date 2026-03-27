namespace GvResearch.Shared.Exceptions;

public class GvAuthException : GvApiException
{
    public GvAuthException()
        : base()
    {
    }

    public GvAuthException(string message)
        : base(message, 401)
    {
    }

    public GvAuthException(string message, Exception innerException)
        : base(message, 401, innerException)
    {
    }

    public GvAuthException(string message, int statusCode = 401)
        : base(message, statusCode)
    {
    }

    public GvAuthException(string message, int statusCode, Exception innerException)
        : base(message, statusCode, innerException)
    {
    }
}
