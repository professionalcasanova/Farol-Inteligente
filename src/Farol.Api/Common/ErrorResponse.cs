namespace Farol.Api.Common;

public sealed class ErrorResponse
{
    public ErrorResponse()
    {
        Error = new ApiErrorResponse("unknown_error", string.Empty);
    }

    public ErrorResponse(string message, string code = "request_error", object? details = null)
    {
        Error = new ApiErrorResponse(code, message, details);
    }

    public ApiErrorResponse Error { get; init; }

    [System.Text.Json.Serialization.JsonIgnore]
    public string Message => Error.Message;
}
