using System.Net.Http.Json;

namespace Farol.Tests.Api;

internal static class ApiTestResponseReader
{
    public static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<SuccessResponse<T>>();

        Assert.NotNull(payload);
        Assert.NotNull(payload.Data);

        return payload.Data;
    }

    private sealed class SuccessResponse<T>
    {
        public T? Data { get; init; }
    }
}
