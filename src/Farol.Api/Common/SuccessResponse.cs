namespace Farol.Api.Common;

public sealed class SuccessResponse<T>
{
    public SuccessResponse(T data)
    {
        Data = data;
    }

    public T Data { get; init; }
}
