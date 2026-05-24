using Farol.Api.Common;
using Farol.Domain.Users;
using Farol.Infrastructure.Auth;
using Farol.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Farol.Api.Modules.Auth;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    FarolDbContext dbContext,
    PasswordService passwordService,
    PasswordResetService passwordResetService,
    RefreshTokenService refreshTokenService) : ControllerBase
{
    private const string RefreshTokenCookieName = "__Host-farol_refresh";

    [AllowAnonymous]
    [EnableRateLimiting("auth-sensitive")]
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new ErrorResponse("Name, email and password are required.", "validation_error"));
        }

        var passwordValidation = passwordService.ValidatePassword(request.Password);

        if (!passwordValidation.IsValid)
        {
            return BadRequest(new ErrorResponse(passwordValidation.ErrorMessage!, "validation_error"));
        }

        var normalizedEmail = NormalizeEmail(request.Email);
        var emailAlreadyInUse = await dbContext.Users
            .AnyAsync(user => user.Email == normalizedEmail, cancellationToken);

        if (emailAlreadyInUse)
        {
            return Conflict(new ErrorResponse("Email is already in use.", "duplicate_email"));
        }

        User user;

        try
        {
            user = new User(request.Name, normalizedEmail, "pending-password-hash");
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ErrorResponse(exception.Message, "validation_error"));
        }

        var passwordHash = passwordService.HashPassword(user, request.Password);
        user.ChangePasswordHash(passwordHash);

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new SuccessResponse<AuthResponse>(await CreateAuthResponseAsync(user, cancellationToken)));
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth-login")]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new ErrorResponse("Email and password are required.", "validation_error"));
        }

        var normalizedEmail = NormalizeEmail(request.Email);
        var user = await dbContext.Users
            .SingleOrDefaultAsync(candidate => candidate.Email == normalizedEmail, cancellationToken);

        if (user is null || !passwordService.VerifyPassword(user, request.Password))
        {
            return Unauthorized(new ErrorResponse("Invalid email or password.", "invalid_credentials"));
        }

        return Ok(new SuccessResponse<AuthResponse>(await CreateAuthResponseAsync(user, cancellationToken)));
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth-sensitive")]
    [HttpPost("forgot-password")]
    public async Task<ActionResult> ForgotPassword(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new ErrorResponse("Email is required.", "validation_error"));
        }

        await passwordResetService.RequestPasswordResetAsync(request.Email, cancellationToken);

        return Ok(new SuccessResponse<object>(new
        {
            message = "If the email exists, password reset instructions have been sent."
        }));
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth-sensitive")]
    [HttpPost("reset-password")]
    public async Task<ActionResult> ResetPassword(
        ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new ErrorResponse("Token and password are required.", "validation_error"));
        }

        var result = await passwordResetService.ResetPasswordAsync(
            request.Token,
            request.Password,
            cancellationToken);

        if (result != PasswordResetResult.Success)
        {
            if (result == PasswordResetResult.WeakPassword)
            {
                return BadRequest(new ErrorResponse(PasswordService.PasswordPolicyErrorMessage, "validation_error"));
            }

            return BadRequest(new ErrorResponse("Password reset token is invalid or expired.", "invalid_reset_token"));
        }

        return Ok(new SuccessResponse<object>(new
        {
            message = "Password has been reset successfully."
        }));
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth-sensitive")]
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(
        RefreshTokenRequest? request,
        CancellationToken cancellationToken)
    {
        var refreshToken = ResolveRefreshToken(request?.RefreshToken);

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return BadRequest(new ErrorResponse("Refresh token is required.", "validation_error"));
        }

        var result = await refreshTokenService.RefreshAsync(refreshToken, cancellationToken);

        if (!result.IsSuccess || result.User is null || result.AccessToken is null || result.RefreshToken is null)
        {
            ClearRefreshTokenCookie();
            return BadRequest(new ErrorResponse("Refresh token is invalid or expired.", "invalid_refresh_token"));
        }

        SetRefreshTokenCookie(result.RefreshToken);

        return Ok(new SuccessResponse<AuthResponse>(new AuthResponse
        {
            AccessToken = result.AccessToken,
            UserId = result.User.Id,
            Name = result.User.Name,
            Email = result.User.Email
        }));
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth-sensitive")]
    [HttpPost("logout")]
    public async Task<ActionResult> Logout(
        LogoutRequest? request,
        CancellationToken cancellationToken)
    {
        var refreshToken = ResolveRefreshToken(request?.RefreshToken);

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return BadRequest(new ErrorResponse("Refresh token is required.", "validation_error"));
        }

        await refreshTokenService.RevokeAsync(refreshToken, cancellationToken);
        ClearRefreshTokenCookie();

        return Ok(new SuccessResponse<object>(new
        {
            message = "Logged out successfully."
        }));
    }

    [Authorize]
    [HttpGet("sessions")]
    public async Task<ActionResult<IReadOnlyList<SessionResponse>>> GetSessions(CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryGetUserId(User, out var userId))
        {
            return Unauthorized(new ErrorResponse("Invalid access token.", "unauthorized"));
        }

        var sessions = await refreshTokenService.ListActiveSessionsAsync(userId, cancellationToken);

        return Ok(new SuccessResponse<IReadOnlyList<SessionResponse>>(sessions
            .Select(SessionResponse.FromRefreshToken)
            .ToList()));
    }

    [Authorize]
    [HttpDelete("sessions/{sessionId:guid}")]
    public async Task<ActionResult> RevokeSession(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryGetUserId(User, out var userId))
        {
            return Unauthorized(new ErrorResponse("Invalid access token.", "unauthorized"));
        }

        var revoked = await refreshTokenService.RevokeSessionAsync(userId, sessionId, cancellationToken);

        if (!revoked)
        {
            return NotFound(new ErrorResponse("Session not found.", "session_not_found"));
        }

        return Ok(new SuccessResponse<object>(new
        {
            message = "Session revoked successfully."
        }));
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<ActionResult> ChangePassword(
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) ||
            string.IsNullOrWhiteSpace(request.NewPassword) ||
            string.IsNullOrWhiteSpace(request.ConfirmNewPassword))
        {
            return BadRequest(new ErrorResponse(
                "Current password, new password and confirmation are required.",
                "validation_error"));
        }

        if (!string.Equals(request.NewPassword, request.ConfirmNewPassword, StringComparison.Ordinal))
        {
            return BadRequest(new ErrorResponse("New password and confirmation must match.", "validation_error"));
        }

        var passwordValidation = passwordService.ValidatePassword(request.NewPassword);

        if (!passwordValidation.IsValid)
        {
            return BadRequest(new ErrorResponse(passwordValidation.ErrorMessage!, "validation_error"));
        }

        if (!AuthenticatedUser.TryGetUserId(User, out var userId))
        {
            return Unauthorized(new ErrorResponse("Invalid access token.", "unauthorized"));
        }

        var user = await dbContext.Users
            .SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);

        if (user is null)
        {
            return Unauthorized(new ErrorResponse("Invalid access token.", "unauthorized"));
        }

        if (!passwordService.VerifyPassword(user, request.CurrentPassword))
        {
            return BadRequest(new ErrorResponse("Current password is invalid.", "invalid_current_password"));
        }

        if (passwordService.VerifyPassword(user, request.NewPassword))
        {
            return BadRequest(new ErrorResponse(
                "New password must be different from the current password.",
                "validation_error"));
        }

        user.ChangePasswordHash(passwordService.HashPassword(user, request.NewPassword));
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new SuccessResponse<object>(new
        {
            message = "Password changed successfully."
        }));
    }

    private async Task<AuthResponse> CreateAuthResponseAsync(User user, CancellationToken cancellationToken)
    {
        var session = await refreshTokenService.CreateSessionAsync(user, cancellationToken);
        SetRefreshTokenCookie(session.RefreshToken);

        return new AuthResponse
        {
            AccessToken = session.AccessToken,
            UserId = user.Id,
            Name = user.Name,
            Email = user.Email
        };
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    private string? ResolveRefreshToken(string? requestRefreshToken)
    {
        if (!string.IsNullOrWhiteSpace(requestRefreshToken))
        {
            return requestRefreshToken;
        }

        return Request.Cookies.TryGetValue(RefreshTokenCookieName, out var cookieRefreshToken)
            ? cookieRefreshToken
            : null;
    }

    private void SetRefreshTokenCookie(string refreshToken)
    {
        Response.Cookies.Append(
            RefreshTokenCookieName,
            refreshToken,
            BuildRefreshCookieOptions(DateTimeOffset.UtcNow.AddDays(7)));
    }

    private void ClearRefreshTokenCookie()
    {
        Response.Cookies.Delete(
            RefreshTokenCookieName,
            BuildRefreshCookieOptions(DateTimeOffset.UnixEpoch));
    }

    private static CookieOptions BuildRefreshCookieOptions(DateTimeOffset expires)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = expires
        };
    }
}
