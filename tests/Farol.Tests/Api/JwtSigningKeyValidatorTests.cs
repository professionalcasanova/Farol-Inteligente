using Farol.Api.Common;

namespace Farol.Tests.Api;

public sealed class JwtSigningKeyValidatorTests
{
    [Fact]
    public void EnsureSafeForEnvironment_ProductionWithDefaultMarker_ShouldThrow()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            JwtSigningKeyValidator.EnsureSafeForEnvironment(
                "farol-default-signing-key-for-production-tests-12345",
                "Production"));

        Assert.Equal("Jwt:SigningKey must be configured with a production secret.", exception.Message);
    }

    [Theory]
    [InlineData("farol-local-signing-key-for-production-tests-12345")]
    [InlineData("farol-development-signing-key-for-production-tests-12345")]
    [InlineData("farol-change-in-production-signing-key-12345")]
    public void EnsureSafeForEnvironment_ProductionWithUnsafeMarker_ShouldThrow(string signingKey)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            JwtSigningKeyValidator.EnsureSafeForEnvironment(signingKey, "Production"));

        Assert.Equal("Jwt:SigningKey must be configured with a production secret.", exception.Message);
    }

    [Fact]
    public void EnsureSafeForEnvironment_ProductionWithShortKey_ShouldThrow()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            JwtSigningKeyValidator.EnsureSafeForEnvironment("short-secret", "Production"));

        Assert.Equal("Jwt:SigningKey must be configured with a production secret.", exception.Message);
    }

    [Fact]
    public void EnsureSafeForEnvironment_ProductionWithStrongKey_ShouldAllow()
    {
        JwtSigningKeyValidator.EnsureSafeForEnvironment(
            "farol-production-signing-key-with-strong-random-material-2026",
            "Production");
    }

    [Fact]
    public void EnsureSafeForEnvironment_DevelopmentWithDefaultMarker_ShouldAllow()
    {
        JwtSigningKeyValidator.EnsureSafeForEnvironment(
            "farol-local-development-signing-key-123456789",
            "Development");
    }
}
