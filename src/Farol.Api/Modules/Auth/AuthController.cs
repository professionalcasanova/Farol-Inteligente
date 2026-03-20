using Farol.Api.Common;
using Farol.Domain.Users;
using Farol.Infrastructure.Auth;
using Farol.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Farol.Api.Modules.Auth;

[ApiController]
[AllowAnonymous]
[Route("api/auth")]
public sealed class AuthController(
    FarolDbContext dbContext,
    PasswordService passwordService,
    JwtTokenService jwtTokenService) : ControllerBase
{
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

        return Ok(CreateAuthResponse(user));
    }

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

        return Ok(CreateAuthResponse(user));
    }

    private AuthResponse CreateAuthResponse(User user)
    {
        return new AuthResponse
        {
            AccessToken = jwtTokenService.CreateAccessToken(user),
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
