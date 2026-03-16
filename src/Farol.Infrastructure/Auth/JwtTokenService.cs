using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Farol.Domain.Users;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Farol.Infrastructure.Auth;

public sealed class JwtTokenService
{
    private readonly string _issuer;
    private readonly string _audience;
    private readonly SigningCredentials _signingCredentials;

    public JwtTokenService(IConfiguration configuration)
    {
        var jwtSection = configuration.GetSection("Jwt");
        var signingKey = jwtSection["SigningKey"]
            ?? throw new InvalidOperationException("Jwt:SigningKey configuration is required.");

        _issuer = jwtSection["Issuer"]
            ?? throw new InvalidOperationException("Jwt:Issuer configuration is required.");

        _audience = jwtSection["Audience"]
            ?? throw new InvalidOperationException("Jwt:Audience configuration is required.");

        _signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);
    }

    public string CreateAccessToken(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email)
        };

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: _signingCredentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
