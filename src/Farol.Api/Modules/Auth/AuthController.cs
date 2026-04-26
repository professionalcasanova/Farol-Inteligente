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
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new ErrorResponse("Name, email and password are required."));
        }

        var passwordValidation = passwordService.ValidatePassword(request.Password);

        if (!passwordValidation.IsValid)
        {
            return BadRequest(new ErrorResponse(passwordValidation.ErrorMessage!));
        }

        var normalizedEmail = NormalizeEmail(request.Email);
        var emailAlreadyInUse = await dbContext.Users
            .AnyAsync(user => user.Email == normalizedEmail, cancellationToken);

        if (emailAlreadyInUse)
        {
            return Conflict(new ErrorResponse("Email is already in use."));
        }

        User user;

        try
        {
            user = new User(request.Name, normalizedEmail, "pending-password-hash");
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ErrorResponse(exception.Message));
        }

        var passwordHash = passwordService.HashPassword(user, request.Password);
        user.ChangePasswordHash(passwordHash);

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(await CreateAuthResponseAsync(user, cancellationToken));
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
            return BadRequest(new ErrorResponse("Email and password are required."));
        }

        var normalizedEmail = NormalizeEmail(request.Email);
        var user = await dbContext.Users
            .SingleOrDefaultAsync(candidate => candidate.Email == normalizedEmail, cancellationToken);

        if (user is null || !passwordService.VerifyPassword(user, request.Password))
        {
            return Unauthorized(new ErrorResponse("Invalid email or password."));
        }

        return Ok(await CreateAuthResponseAsync(user, cancellationToken));
    }

    [AllowAnonymous]
    [HttpPost("forgot-password")]
    public async Task<ActionResult> ForgotPassword(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new ErrorResponse("Email is required."));
        }

        await passwordResetService.RequestPasswordResetAsync(request.Email, cancellationToken);

        return Ok(new
        {
            message = "If the email exists, a password reset token has been generated."
        });
    }

    [AllowAnonymous]
    [HttpPost("reset-password")]
    public async Task<ActionResult> ResetPassword(
        ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new ErrorResponse("Token and password are required."));
        }

        var result = await passwordResetService.ResetPasswordAsync(
            request.Token,
            request.Password,
            cancellationToken);

        if (result != PasswordResetResult.Success)
        {
            if (result == PasswordResetResult.WeakPassword)
            {
                return BadRequest(new ErrorResponse(PasswordService.PasswordPolicyErrorMessage));
            }

            return BadRequest(new ErrorResponse("Password reset token is invalid or expired."));
        }

        return Ok(new
        {
            message = "Password has been reset successfully."
        });
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(
        RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return BadRequest(new ErrorResponse("Refresh token is required."));
        }

        var result = await refreshTokenService.RefreshAsync(request.RefreshToken, cancellationToken);

        if (!result.IsSuccess || result.User is null || result.AccessToken is null || result.RefreshToken is null)
        {
            return BadRequest(new ErrorResponse("Refresh token is invalid or expired."));
        }

        return Ok(new AuthResponse
        {
            AccessToken = result.AccessToken,
            RefreshToken = result.RefreshToken,
            UserId = result.User.Id,
            Name = result.User.Name,
            Email = result.User.Email
        });
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<ActionResult> Logout(
        LogoutRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return BadRequest(new ErrorResponse("Refresh token is required."));
        }

        await refreshTokenService.RevokeAsync(request.RefreshToken, cancellationToken);

        return Ok(new
        {
            message = "Logged out successfully."
        });
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
            return BadRequest(new ErrorResponse("Current password, new password and confirmation are required."));
        }

        if (!string.Equals(request.NewPassword, request.ConfirmNewPassword, StringComparison.Ordinal))
        {
            return BadRequest(new ErrorResponse("New password and confirmation must match."));
        }

        var passwordValidation = passwordService.ValidatePassword(request.NewPassword);

        if (!passwordValidation.IsValid)
        {
            return BadRequest(new ErrorResponse(passwordValidation.ErrorMessage!));
        }

        if (!AuthenticatedUser.TryGetUserId(User, out var userId))
        {
            return Unauthorized(new ErrorResponse("Invalid access token."));
        }

        var user = await dbContext.Users
            .SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);

        if (user is null)
        {
            return Unauthorized(new ErrorResponse("Invalid access token."));
        }

        if (!passwordService.VerifyPassword(user, request.CurrentPassword))
        {
            return BadRequest(new ErrorResponse("Current password is invalid."));
        }

        if (passwordService.VerifyPassword(user, request.NewPassword))
        {
            return BadRequest(new ErrorResponse("New password must be different from the current password."));
        }

        user.ChangePasswordHash(passwordService.HashPassword(user, request.NewPassword));
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Password changed successfully."
        });
    }

    private async Task<AuthResponse> CreateAuthResponseAsync(User user, CancellationToken cancellationToken)
    {
        var session = await refreshTokenService.CreateSessionAsync(user, cancellationToken);

        return new AuthResponse
        {
            AccessToken = session.AccessToken,
            RefreshToken = session.RefreshToken,
            UserId = user.Id,
            Name = user.Name,
            Email = user.Email
        };
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }
}
