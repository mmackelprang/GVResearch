namespace GvResearch.Shared.Exceptions;

public class GvAuthException : GvApiException
{
    public GvAuthException(string message, int statusCode = 401)
        : base(message, statusCode)
    {
    }

    public GvAuthException(string message, int statusCode, Exception innerException)
        : base(message, statusCode, innerException)
    {
    }
}
