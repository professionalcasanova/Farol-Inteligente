using System.Net;
using System.Net.Http.Json;
using Farol.Api.Modules.Auth;
using Microsoft.AspNetCore.Http;

namespace Farol.Tests.Api;

public sealed class AuthEndpointsTests : IClassFixture<FarolApiFactory>
{
    private readonly FarolApiFactory _factory;

    public AuthEndpointsTests(FarolApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Register_ValidPayload_ReturnsSuccess()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Name = "Maria Silva",
            Email = "maria@email.com",
            Password = "123456"
        });

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<AuthResponse>();

        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload.AccessToken));
        Assert.NotEqual(Guid.Empty, payload.UserId);
        Assert.Equal("Maria Silva", payload.Name);
        Assert.Equal("maria@email.com", payload.Email);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsConflict()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        await RegisterAsync(client, "Maria Silva", "maria@email.com", "123456");

        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Name = "Outra Maria",
            Email = "MARIA@email.com",
            Password = "abcdef"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        Assert.NotNull(payload);
        Assert.Equal("Email is already in use.", payload.Message);
    }

    [Fact]
    public async Task Register_InvalidPayload_ReturnsBadRequest()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Name = " ",
            Email = "maria@email.com",
            Password = "123456"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();

        Assert.NotNull(payload);
        Assert.Equal("One or more validation errors occurred.", payload.Title);
        Assert.Contains(nameof(RegisterRequest.Name), payload.Errors.Keys);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsJwtToken()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        await RegisterAsync(client, "Maria Silva", "maria@email.com", "123456");

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = "maria@email.com",
            Password = "123456"
        });

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<AuthResponse>();

        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload.AccessToken));
        Assert.Equal("Maria Silva", payload.Name);
        Assert.Equal("maria@email.com", payload.Email);
    }

    [Fact]
    public async Task Login_InvalidPassword_ReturnsUnauthorized()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        await RegisterAsync(client, "Maria Silva", "maria@email.com", "123456");

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = "maria@email.com",
            Password = "senha-errada"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        Assert.NotNull(payload);
        Assert.Equal("Invalid email or password.", payload.Message);
    }

    [Fact]
    public async Task Login_NonexistentUser_ReturnsUnauthorized()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = "naoexiste@email.com",
            Password = "123456"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        Assert.NotNull(payload);
        Assert.Equal("Invalid email or password.", payload.Message);
    }

    [Fact]
    public async Task Login_EmailWithDifferentCase_ReturnsSuccess()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        await RegisterAsync(client, "Maria Silva", "maria@email.com", "123456");

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = "MARIA@EMAIL.COM",
            Password = "123456"
        });

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<AuthResponse>();

        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload.AccessToken));
        Assert.Equal("maria@email.com", payload.Email);
    }

    private static async Task<AuthResponse> RegisterAsync(
        HttpClient client,
        string name,
        string email,
        string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Name = name,
            Email = email,
            Password = password
        });

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<AuthResponse>();

        Assert.NotNull(payload);

        return payload;
    }

    private sealed class ErrorResponse
    {
        public string? Message { get; init; }
    }
}
