using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Farol.Api.Modules.Auth;
using Farol.Domain.Users;
using Farol.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Farol.Tests.Api;

public sealed class AuthEndpointsTests : IClassFixture<FarolApiFactory>
{
    private const string StrongPassword = "Password123";
    private const string AnotherStrongPassword = "Password456";
    private const string ThirdStrongPassword = "Password789";
    private const string PasswordPolicyMessage =
        "Password must be at least 8 characters long, contain at least 1 letter and 1 number, and cannot be only spaces.";
    private const string LoginRateLimitMessage = "Muitas tentativas. Tente novamente em alguns instantes.";

    private readonly FarolApiFactory _factory;

    public AuthEndpointsTests(FarolApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Register_ValidPayload_ShouldReturnSuccess()
    {
        await _factory.ResetDatabaseAsync();
        using var client = CreateIsolatedClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Name = "Maria Silva",
            Email = "maria@email.com",
            Password = StrongPassword
        });

        response.EnsureSuccessStatusCode();

        var payload = await ReadDataAsync<AuthResponse>(response);

        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload.AccessToken));
        Assert.NotEqual(Guid.Empty, payload.UserId);
        Assert.Equal("Maria Silva", payload.Name);
        Assert.Equal("maria@email.com", payload.Email);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ShouldReturnConflict()
    {
        await _factory.ResetDatabaseAsync();
        using var client = CreateIsolatedClient();

        await RegisterAsync(client, "Maria Silva", "maria@email.com", StrongPassword);

        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Name = "Outra Maria",
            Email = "MARIA@email.com",
            Password = AnotherStrongPassword
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        Assert.NotNull(payload);
        Assert.Equal("Email is already in use.", payload.Message);
    }

    [Fact]
    public async Task Register_InvalidPayload_ShouldReturnBadRequest()
    {
        await _factory.ResetDatabaseAsync();
        using var client = CreateIsolatedClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Name = " ",
            Email = "maria@email.com",
            Password = StrongPassword
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        Assert.NotNull(payload);
        Assert.Contains("Name", payload.Message);
    }

    [Fact]
    public async Task Register_WeakPassword_ShouldReturnBadRequest()
    {
        await _factory.ResetDatabaseAsync();
        using var client = CreateIsolatedClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Name = "Maria Silva",
            Email = "maria@email.com",
            Password = "1234567"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        Assert.NotNull(payload);
        Assert.Equal(PasswordPolicyMessage, payload.Message);
    }

    [Fact]
    public async Task Login_ValidCredentials_ShouldReturnJwtToken()
    {
        await _factory.ResetDatabaseAsync();
        using var client = CreateClientWithIp("10.0.0.1");

        await RegisterAsync(client, "Maria Silva", "maria@email.com", StrongPassword);

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = "maria@email.com",
            Password = StrongPassword
        });

        response.EnsureSuccessStatusCode();

        var payload = await ReadDataAsync<AuthResponse>(response);

        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(payload.RefreshToken));
        Assert.Equal("Maria Silva", payload.Name);
        Assert.Equal("maria@email.com", payload.Email);
    }

    [Fact]
    public async Task Login_InvalidPassword_ShouldReturnUnauthorized()
    {
        await _factory.ResetDatabaseAsync();
        using var client = CreateClientWithIp("10.0.0.2");

        await RegisterAsync(client, "Maria Silva", "maria@email.com", StrongPassword);

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
    public async Task Login_NonexistentUser_ShouldReturnUnauthorized()
    {
        await _factory.ResetDatabaseAsync();
        using var client = CreateClientWithIp("10.0.0.3");

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = "naoexiste@email.com",
            Password = StrongPassword
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        Assert.NotNull(payload);
        Assert.Equal("Invalid email or password.", payload.Message);
    }

    [Fact]
    public async Task Login_EmailWithDifferentCase_ShouldReturnSuccess()
    {
        await _factory.ResetDatabaseAsync();
        using var client = CreateClientWithIp("10.0.0.4");

        await RegisterAsync(client, "Maria Silva", "maria@email.com", StrongPassword);

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = "MARIA@EMAIL.COM",
            Password = StrongPassword
        });

        response.EnsureSuccessStatusCode();

        var payload = await ReadDataAsync<AuthResponse>(response);

        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(payload.RefreshToken));
        Assert.Equal("maria@email.com", payload.Email);
    }

    [Fact]
    public async Task Login_TooManyAttempts_ShouldReturnTooManyRequests()
    {
        await _factory.ResetDatabaseAsync();
        using var client = CreateClientWithIp("10.0.0.5");

        await RegisterAsync(client, "Maria Silva", "maria@email.com", StrongPassword);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var invalidResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
            {
                Email = "maria@email.com",
                Password = "senha-errada"
            });

            Assert.Equal(HttpStatusCode.Unauthorized, invalidResponse.StatusCode);
        }

        var rateLimitedResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = "maria@email.com",
            Password = "senha-errada"
        });

        Assert.Equal((HttpStatusCode)429, rateLimitedResponse.StatusCode);

        var payload = await rateLimitedResponse.Content.ReadFromJsonAsync<ErrorResponse>();

        Assert.NotNull(payload);
        Assert.Equal(LoginRateLimitMessage, payload.Message);
    }

    [Fact]
    public async Task Login_RateLimit_ShouldApplyOnlyToLogin()
    {
        await _factory.ResetDatabaseAsync();
        using var client = CreateClientWithIp("10.0.0.6");

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var invalidResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
            {
                Email = "naoexiste@email.com",
                Password = "senha-errada"
            });

            Assert.Equal(HttpStatusCode.Unauthorized, invalidResponse.StatusCode);
        }

        var rateLimitedResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = "naoexiste@email.com",
            Password = "senha-errada"
        });

        Assert.Equal((HttpStatusCode)429, rateLimitedResponse.StatusCode);

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Name = "Maria Silva",
            Email = "maria@email.com",
            Password = StrongPassword
        });

        registerResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Refresh_ValidToken_ShouldRotateRefreshTokenAndReturnNewTokens()
    {
        await _factory.ResetDatabaseAsync();
        using var client = CreateIsolatedClient();

        await RegisterAsync(client, "Maria Silva", "maria@email.com", StrongPassword);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = "maria@email.com",
            Password = StrongPassword
        });

        loginResponse.EnsureSuccessStatusCode();

        var loginPayload = await ReadDataAsync<AuthResponse>(loginResponse);

        Assert.NotNull(loginPayload);

        var refreshResponse = await client.PostAsJsonAsync("/api/auth/refresh", new
        {
            refreshToken = loginPayload.RefreshToken
        });

        refreshResponse.EnsureSuccessStatusCode();

        var refreshPayload = await ReadDataAsync<AuthResponse>(refreshResponse);

        Assert.NotNull(refreshPayload);
        Assert.False(string.IsNullOrWhiteSpace(refreshPayload.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(refreshPayload.RefreshToken));
        Assert.NotEqual(loginPayload.RefreshToken, refreshPayload.RefreshToken);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();
        var oldToken = dbContext.RefreshTokens.Single(token => token.Token == loginPayload.RefreshToken);
        var rotatedToken = dbContext.RefreshTokens.Single(token => token.Token == refreshPayload.RefreshToken);

        Assert.True(oldToken.Revoked);
        Assert.False(rotatedToken.Revoked);
    }

    [Fact]
    public async Task Refresh_InvalidToken_ShouldReturnBadRequest()
    {
        await _factory.ResetDatabaseAsync();
        using var client = CreateIsolatedClient();

        var response = await client.PostAsJsonAsync("/api/auth/refresh", new
        {
            refreshToken = "invalid-token"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        Assert.NotNull(payload);
        Assert.Equal("Refresh token is invalid or expired.", payload.Message);
    }

    [Fact]
    public async Task Refresh_ExpiredToken_ShouldReturnBadRequest()
    {
        await _factory.ResetDatabaseAsync();
        using var client = CreateIsolatedClient();

        var auth = await RegisterAsync(client, "Maria Silva", "maria@email.com", StrongPassword);

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();
            dbContext.RefreshTokens.Add(new RefreshToken(
                auth.UserId,
                "expired-token",
                new DateTimeOffset(2026, 3, 10, 10, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 3, 10, 11, 0, 0, TimeSpan.Zero)));
            await dbContext.SaveChangesAsync();
        }

        var response = await client.PostAsJsonAsync("/api/auth/refresh", new
        {
            refreshToken = "expired-token"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        Assert.NotNull(payload);
        Assert.Equal("Refresh token is invalid or expired.", payload.Message);
    }

    [Fact]
    public async Task Refresh_RevokedToken_ShouldReturnBadRequest()
    {
        await _factory.ResetDatabaseAsync();
        using var client = CreateIsolatedClient();

        var auth = await RegisterAsync(client, "Maria Silva", "maria@email.com", StrongPassword);

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();
            var refreshToken = new RefreshToken(
                auth.UserId,
                "revoked-token",
                new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 3, 17, 12, 0, 0, TimeSpan.Zero));
            refreshToken.Revoke();
            dbContext.RefreshTokens.Add(refreshToken);
            await dbContext.SaveChangesAsync();
        }

        var response = await client.PostAsJsonAsync("/api/auth/refresh", new
        {
            refreshToken = "revoked-token"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        Assert.NotNull(payload);
        Assert.Equal("Refresh token is invalid or expired.", payload.Message);
    }

    [Fact]
    public async Task Logout_ValidRefreshToken_ShouldRevokeToken()
    {
        await _factory.ResetDatabaseAsync();
        using var client = CreateIsolatedClient();

        await RegisterAsync(client, "Maria Silva", "maria@email.com", StrongPassword);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = "maria@email.com",
            Password = StrongPassword
        });

        loginResponse.EnsureSuccessStatusCode();

        var loginPayload = await ReadDataAsync<AuthResponse>(loginResponse);

        Assert.NotNull(loginPayload);

        var logoutResponse = await client.PostAsJsonAsync("/api/auth/logout", new
        {
            refreshToken = loginPayload.RefreshToken
        });

        logoutResponse.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();
        var refreshToken = dbContext.RefreshTokens.Single(token => token.Token == loginPayload.RefreshToken);

        Assert.True(refreshToken.Revoked);
    }

    [Fact]
    public async Task Sessions_AuthenticatedUser_ShouldListOnlyOwnActiveSessions()
    {
        await _factory.ResetDatabaseAsync();
        using var client = CreateIsolatedClient();

        var mariaAuth = await RegisterAsync(client, "Maria Silva", "maria@email.com", StrongPassword);
        await RegisterAsync(client, "Joao Souza", "joao@email.com", AnotherStrongPassword);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mariaAuth.AccessToken);

        var response = await client.GetAsync("/api/auth/sessions");

        response.EnsureSuccessStatusCode();

        var sessions = await ReadDataAsync<List<SessionResponse>>(response);

        Assert.NotNull(sessions);
        Assert.Single(sessions);
        Assert.False(sessions[0].Revoked);
    }

    [Fact]
    public async Task Sessions_ExpiredOrRevokedSessions_ShouldNotReturnAsActive()
    {
        await _factory.ResetDatabaseAsync();
        using var client = CreateIsolatedClient();

        var auth = await RegisterAsync(client, "Maria Silva", "maria@email.com", StrongPassword);

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();
            var expiredToken = new RefreshToken(
                auth.UserId,
                "expired-session-token",
                new DateTimeOffset(2026, 3, 10, 10, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 3, 10, 11, 0, 0, TimeSpan.Zero));
            var revokedToken = new RefreshToken(
                auth.UserId,
                "revoked-session-token",
                new DateTimeOffset(2026, 3, 10, 12, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 3, 17, 12, 0, 0, TimeSpan.Zero));
            revokedToken.Revoke();

            dbContext.RefreshTokens.Add(expiredToken);
            dbContext.RefreshTokens.Add(revokedToken);
            await dbContext.SaveChangesAsync();
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var response = await client.GetAsync("/api/auth/sessions");

        response.EnsureSuccessStatusCode();

        var sessions = await ReadDataAsync<List<SessionResponse>>(response);

        Assert.NotNull(sessions);
        Assert.Single(sessions);
    }

    [Fact]
    public async Task Sessions_OwnSession_ShouldRevokeSession()
    {
        await _factory.ResetDatabaseAsync();
        using var client = CreateIsolatedClient();

        var auth = await RegisterAsync(client, "Maria Silva", "maria@email.com", StrongPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var sessionsResponse = await client.GetAsync("/api/auth/sessions");
        sessionsResponse.EnsureSuccessStatusCode();
        var sessions = await ReadDataAsync<List<SessionResponse>>(sessionsResponse);

        Assert.NotNull(sessions);

        var deleteResponse = await client.DeleteAsync($"/api/auth/sessions/{sessions[0].Id}");

        deleteResponse.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();
        var refreshToken = dbContext.RefreshTokens.Single(token => token.Id == sessions[0].Id);

        Assert.True(refreshToken.Revoked);
    }

    [Fact]
    public async Task Sessions_OtherUserSession_ShouldNotRevokeSession()
    {
        await _factory.ResetDatabaseAsync();
        using var client = CreateIsolatedClient();

        var mariaAuth = await RegisterAsync(client, "Maria Silva", "maria@email.com", StrongPassword);
        var joaoAuth = await RegisterAsync(client, "Joao Souza", "joao@email.com", AnotherStrongPassword);

        Guid joaoSessionId;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();
            joaoSessionId = dbContext.RefreshTokens.Single(token => token.Token == joaoAuth.RefreshToken).Id;
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mariaAuth.AccessToken);

        var deleteResponse = await client.DeleteAsync($"/api/auth/sessions/{joaoSessionId}");

        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);

        using var verificationScope = _factory.Services.CreateScope();
        var verificationDbContext = verificationScope.ServiceProvider.GetRequiredService<FarolDbContext>();
        var joaoToken = verificationDbContext.RefreshTokens.Single(token => token.Id == joaoSessionId);

        Assert.False(joaoToken.Revoked);
    }

    [Fact]
    public async Task Sessions_Response_ShouldNotExposeRefreshTokenValue()
    {
        await _factory.ResetDatabaseAsync();
        using var client = CreateIsolatedClient();

        var auth = await RegisterAsync(client, "Maria Silva", "maria@email.com", StrongPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var response = await client.GetAsync("/api/auth/sessions");

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var session = document.RootElement.GetProperty("data").EnumerateArray().Single();

        Assert.False(session.TryGetProperty("token", out _));
        Assert.False(session.TryGetProperty("refreshToken", out _));
        Assert.DoesNotContain(auth.RefreshToken, json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ForgotPassword_ExistingEmail_ShouldReturnSuccessAndCreateToken()
    {
        await _factory.ResetDatabaseAsync();
        using var client = CreateIsolatedClient();

        await RegisterAsync(client, "Maria Silva", "maria@email.com", StrongPassword);

        var response = await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest
        {
            Email = "maria@email.com"
        });

        response.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();
        var token = dbContext.PasswordResetTokens.Single();

        Assert.False(string.IsNullOrWhiteSpace(token.Token));
        Assert.False(token.Used);
        Assert.NotEqual(default, token.ExpiresAtUtc);
    }

    [Fact]
    public async Task ForgotPassword_UnknownEmail_ShouldReturnSuccessWithoutCreatingToken()
    {
        await _factory.ResetDatabaseAsync();
        using var client = CreateIsolatedClient();

        var response = await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest
        {
            Email = "naoexiste@email.com"
        });

        response.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();

        Assert.Empty(dbContext.PasswordResetTokens);
    }

    [Fact]
    public async Task ResetPassword_ValidToken_ShouldChangePasswordAndMarkTokenAsUsed()
    {
        await _factory.ResetDatabaseAsync();
        using var client = CreateIsolatedClient();

        await RegisterAsync(client, "Maria Silva", "maria@email.com", StrongPassword);

        await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest
        {
            Email = "maria@email.com"
        });

        string tokenValue;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();
            tokenValue = dbContext.PasswordResetTokens.Single().Token;
        }

        var resetResponse = await client.PostAsJsonAsync("/api/auth/reset-password", new ResetPasswordRequest
        {
            Token = tokenValue,
            Password = AnotherStrongPassword
        });

        resetResponse.EnsureSuccessStatusCode();

        var oldPasswordLogin = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = "maria@email.com",
            Password = StrongPassword
        });

        Assert.Equal(HttpStatusCode.Unauthorized, oldPasswordLogin.StatusCode);

        var newPasswordLogin = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = "maria@email.com",
            Password = AnotherStrongPassword
        });

        newPasswordLogin.EnsureSuccessStatusCode();

        using var verificationScope = _factory.Services.CreateScope();
        var verificationDbContext = verificationScope.ServiceProvider.GetRequiredService<FarolDbContext>();
        var token = verificationDbContext.PasswordResetTokens.Single();

        Assert.True(token.Used);
    }

    [Fact]
    public async Task ResetPassword_UsedToken_ShouldReturnBadRequest()
    {
        await _factory.ResetDatabaseAsync();
        using var client = CreateIsolatedClient();

        await RegisterAsync(client, "Maria Silva", "maria@email.com", StrongPassword);

        await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest
        {
            Email = "maria@email.com"
        });

        string tokenValue;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();
            tokenValue = dbContext.PasswordResetTokens.Single().Token;
        }

        var firstReset = await client.PostAsJsonAsync("/api/auth/reset-password", new ResetPasswordRequest
        {
            Token = tokenValue,
            Password = AnotherStrongPassword
        });

        firstReset.EnsureSuccessStatusCode();

        var secondReset = await client.PostAsJsonAsync("/api/auth/reset-password", new ResetPasswordRequest
        {
            Token = tokenValue,
            Password = ThirdStrongPassword
        });

        Assert.Equal(HttpStatusCode.BadRequest, secondReset.StatusCode);

        var payload = await secondReset.Content.ReadFromJsonAsync<ErrorResponse>();

        Assert.NotNull(payload);
        Assert.Equal("Password reset token is invalid or expired.", payload.Message);
    }

    [Fact]
    public async Task ResetPassword_WeakPassword_ShouldReturnBadRequest()
    {
        await _factory.ResetDatabaseAsync();
        using var client = CreateIsolatedClient();

        await RegisterAsync(client, "Maria Silva", "maria@email.com", StrongPassword);

        await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest
        {
            Email = "maria@email.com"
        });

        string tokenValue;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();
            tokenValue = dbContext.PasswordResetTokens.Single().Token;
        }

        var resetResponse = await client.PostAsJsonAsync("/api/auth/reset-password", new ResetPasswordRequest
        {
            Token = tokenValue,
            Password = "1234567"
        });

        Assert.Equal(HttpStatusCode.BadRequest, resetResponse.StatusCode);

        var payload = await resetResponse.Content.ReadFromJsonAsync<ErrorResponse>();

        Assert.NotNull(payload);
        Assert.Equal(PasswordPolicyMessage, payload.Message);
    }

    [Fact]
    public async Task ChangePassword_ValidPayload_ShouldChangePassword()
    {
        await _factory.ResetDatabaseAsync();
        using var client = CreateIsolatedClient();

        var auth = await RegisterAsync(client, "Maria Silva", "maria@email.com", StrongPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var response = await client.PostAsJsonAsync("/api/auth/change-password", new ChangePasswordRequest
        {
            CurrentPassword = StrongPassword,
            NewPassword = AnotherStrongPassword,
            ConfirmNewPassword = AnotherStrongPassword
        });

        response.EnsureSuccessStatusCode();

        client.DefaultRequestHeaders.Authorization = null;

        var oldPasswordLogin = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = "maria@email.com",
            Password = StrongPassword
        });

        Assert.Equal(HttpStatusCode.Unauthorized, oldPasswordLogin.StatusCode);

        var newPasswordLogin = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = "maria@email.com",
            Password = AnotherStrongPassword
        });

        newPasswordLogin.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ChangePassword_InvalidCurrentPassword_ShouldReturnBadRequest()
    {
        await _factory.ResetDatabaseAsync();
        using var client = CreateIsolatedClient();

        var auth = await RegisterAsync(client, "Maria Silva", "maria@email.com", StrongPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var response = await client.PostAsJsonAsync("/api/auth/change-password", new ChangePasswordRequest
        {
            CurrentPassword = "wrong-password",
            NewPassword = AnotherStrongPassword,
            ConfirmNewPassword = AnotherStrongPassword
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        Assert.NotNull(payload);
        Assert.Equal("Current password is invalid.", payload.Message);
    }

    [Fact]
    public async Task ChangePassword_MismatchedConfirmation_ShouldReturnBadRequest()
    {
        await _factory.ResetDatabaseAsync();
        using var client = CreateIsolatedClient();

        var auth = await RegisterAsync(client, "Maria Silva", "maria@email.com", StrongPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var response = await client.PostAsJsonAsync("/api/auth/change-password", new ChangePasswordRequest
        {
            CurrentPassword = StrongPassword,
            NewPassword = AnotherStrongPassword,
            ConfirmNewPassword = ThirdStrongPassword
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        Assert.NotNull(payload);
        Assert.Equal("New password and confirmation must match.", payload.Message);
    }

    [Fact]
    public async Task ChangePassword_WeakPassword_ShouldReturnBadRequest()
    {
        await _factory.ResetDatabaseAsync();
        using var client = CreateIsolatedClient();

        var auth = await RegisterAsync(client, "Maria Silva", "maria@email.com", StrongPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var response = await client.PostAsJsonAsync("/api/auth/change-password", new ChangePasswordRequest
        {
            CurrentPassword = StrongPassword,
            NewPassword = "1234567",
            ConfirmNewPassword = "1234567"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        Assert.NotNull(payload);
        Assert.Equal(PasswordPolicyMessage, payload.Message);
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

        var payload = await ReadDataAsync<AuthResponse>(response);

        Assert.NotNull(payload);

        return payload;
    }

    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
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

    private sealed class ErrorResponse
    {
        public ApiError? Error { get; init; }
        public string? Message => Error?.Message;
    }

    private sealed class ApiError
    {
        public string? Code { get; init; }
        public string? Message { get; init; }
    }

    private sealed class SessionResponse
    {
        public Guid Id { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset ExpiresAt { get; init; }
        public bool Revoked { get; init; }
    }

    private HttpClient CreateClientWithIp(string ipAddress)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", ipAddress);
        return client;
    }

    private HttpClient CreateIsolatedClient()
    {
        return CreateClientWithIp($"test-{Guid.NewGuid():N}");
    }
}

