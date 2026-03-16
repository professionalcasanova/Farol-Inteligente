using System.IdentityModel.Tokens.Jwt;
using Farol.Domain.Users;
using Farol.Infrastructure.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Farol.Tests.Auth;

public sealed class JwtTokenServiceTests
{
    [Fact]
    public void CreateAccessToken_ShouldGenerateValidToken()
    {
        var service = CreateService();
        var user = new User("Maria", "maria@email.com", "hash");

        var token = service.CreateAccessToken(user);

        var handler = new JwtSecurityTokenHandler();
        var principal = handler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "Farol.Tests",
            ValidateAudience = true,
            ValidAudience = "Farol.Tests.Client",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey("farol-test-signing-key-1234567890"u8.ToArray()),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        }, out _);

        Assert.True(principal.Identity?.IsAuthenticated ?? false);
    }

    [Fact]
    public void CreateAccessToken_ShouldIncludeSubAndEmailClaims()
    {
        var service = CreateService();
        var user = new User("Maria", "maria@email.com", "hash");

        var token = service.CreateAccessToken(user);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal(user.Id.ToString(), jwt.Claims.Single(claim => claim.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal(user.Email, jwt.Claims.Single(claim => claim.Type == JwtRegisteredClaimNames.Email).Value);
    }

    private static JwtTokenService CreateService()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "Farol.Tests",
                ["Jwt:Audience"] = "Farol.Tests.Client",
                ["Jwt:SigningKey"] = "farol-test-signing-key-1234567890"
            })
            .Build();

        return new JwtTokenService(configuration);
    }
}
