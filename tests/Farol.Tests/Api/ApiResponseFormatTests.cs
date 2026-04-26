using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Farol.Api.Modules.Auth;

namespace Farol.Tests.Api;

public sealed class ApiResponseFormatTests : IClassFixture<FarolApiFactory>
{
    private readonly FarolApiFactory _factory;

    public ApiResponseFormatTests(FarolApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AuthSuccess_ValidRegister_ShouldReturnDataEnvelope()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Name = "Maria Silva",
            Email = "maria@email.com",
            Password = "Password123"
        });

        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("data", out var data));
        Assert.True(data.TryGetProperty("accessToken", out _));
        Assert.False(root.TryGetProperty("accessToken", out _));
        Assert.False(root.TryGetProperty("error", out _));
    }

    [Fact]
    public async Task AuthError_InvalidLogin_ShouldReturnErrorEnvelope()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = "missing@email.com",
            Password = "Password123"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var error = await ReadErrorAsync(response);

        Assert.Equal("invalid_credentials", error.Code);
        Assert.Equal("Invalid email or password.", error.Message);
    }

    [Fact]
    public async Task Unauthorized_MissingBearerToken_ShouldReturnErrorEnvelope()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/auth/sessions");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var error = await ReadErrorAsync(response);

        Assert.Equal("unauthorized", error.Code);
        Assert.Equal("Invalid access token.", error.Message);
    }

    [Fact]
    public async Task BadRequest_MissingRefreshToken_ShouldReturnErrorEnvelope()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/refresh", new
        {
            refreshToken = " "
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await ReadErrorAsync(response);

        Assert.Equal("validation_error", error.Code);
        Assert.False(string.IsNullOrWhiteSpace(error.Message));
    }

    [Fact]
    public async Task ServerError_UnhandledException_ShouldReturnErrorEnvelopeWithoutStackTrace()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/test/errors/throw");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var error = ReadError(body);

        Assert.Equal("internal_error", error.Code);
        Assert.Equal("An unexpected error occurred.", error.Message);
        Assert.DoesNotContain("Sensitive internal exception detail", body, StringComparison.Ordinal);
        Assert.DoesNotContain("stackTrace", body, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<ApiError> ReadErrorAsync(HttpResponseMessage response)
    {
        return ReadError(await response.Content.ReadAsStringAsync());
    }

    private static ApiError ReadError(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("error", out var error));
        Assert.False(root.TryGetProperty("data", out _));

        return new ApiError(
            error.GetProperty("code").GetString()!,
            error.GetProperty("message").GetString()!);
    }

    private sealed record ApiError(string Code, string Message);
}
