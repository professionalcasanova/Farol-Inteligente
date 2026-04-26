using System.Text.Json.Serialization;

namespace Farol.Api.Common;

public sealed class ApiErrorResponse
{
    public ApiErrorResponse(string code, string message, object? details = null)
    {
        Code = code;
        Message = message;
        Details = details;
    }

    public string Code { get; init; }
    public string Message { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Details { get; init; }
}
