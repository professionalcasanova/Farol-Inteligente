namespace Farol.Api.Common;

public static class JwtSigningKeyValidator
{
    private const int MinimumProductionKeyLength = 32;

    private static readonly string[] UnsafeProductionKeyMarkers =
    [
        "default",
        "local",
        "development",
        "change-in-production",
    ];

    public static void EnsureSafeForEnvironment(string signingKey, string environmentName)
    {
        if (string.IsNullOrWhiteSpace(signingKey))
        {
            throw new InvalidOperationException("Jwt:SigningKey configuration is required.");
        }

        if (IsRelaxedEnvironment(environmentName))
        {
            return;
        }

        if (signingKey.Trim().Length < MinimumProductionKeyLength ||
            UnsafeProductionKeyMarkers.Any(marker =>
                signingKey.Contains(marker, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Jwt:SigningKey must be configured with a production secret.");
        }
    }

    private static bool IsRelaxedEnvironment(string environmentName)
    {
        return string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase);
    }
}
