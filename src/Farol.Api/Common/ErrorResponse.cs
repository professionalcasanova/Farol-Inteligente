namespace Farol.Api.Common;

public sealed class ErrorResponse
{
    public ErrorResponse(string message)
    {
        Message = message;
    }

    public string Message { get; init; }
}
