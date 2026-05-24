namespace Farol.Infrastructure.Email;

public static class EmailOptionsValidator
{
    public static void EnsureSafeForEnvironment(EmailOptions options, string environmentName)
    {
        EnsureCommonConfiguration(options);

        if (!IsProductionEnvironment(environmentName))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(options.FromAddress))
        {
            throw new InvalidOperationException("Email:FromAddress configuration is required.");
        }

        if (!Uri.TryCreate(options.PublicBaseUrl, UriKind.Absolute, out var publicBaseUrl))
        {
            throw new InvalidOperationException("Email:PublicBaseUrl must be an absolute URL.");
        }

        if (!string.Equals(publicBaseUrl.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Email:PublicBaseUrl must use HTTPS in production.");
        }

        if (IsResendMode(options))
        {
            EnsureProductionResendConfiguration(options);
            return;
        }

        EnsureProductionSmtpConfiguration(options);
    }

    private static void EnsureCommonConfiguration(EmailOptions options)
    {
        if (options.ResetTokenMinutes <= 0)
        {
            throw new InvalidOperationException("Email:ResetTokenMinutes must be greater than zero.");
        }

        if (!IsSmtpMode(options) && !IsResendMode(options))
        {
            throw new InvalidOperationException("Email:Mode must be Smtp or Resend.");
        }
    }

    private static void EnsureProductionResendConfiguration(EmailOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Resend.ApiKey))
        {
            throw new InvalidOperationException(
                "Email:Resend:ApiKey configuration is required when Email:Mode is Resend.");
        }

        if (!Uri.TryCreate(options.Resend.ApiUrl, UriKind.Absolute, out var apiUrl) ||
            !string.Equals(apiUrl.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Email:Resend:ApiUrl must be an absolute HTTPS URL.");
        }
    }

    private static void EnsureProductionSmtpConfiguration(EmailOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Smtp.Host))
        {
            throw new InvalidOperationException("Email:Smtp:Host configuration is required when Email:Mode is Smtp.");
        }

        if (string.Equals(options.Smtp.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(options.Smtp.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Email:Smtp:Host cannot point to local Mailpit in production.");
        }

        if (options.Smtp.Port <= 0)
        {
            throw new InvalidOperationException("Email:Smtp:Port must be greater than zero.");
        }

        if (!options.Smtp.UseTls)
        {
            throw new InvalidOperationException("Email:Smtp:UseTls must be true in production.");
        }
    }

    private static bool IsProductionEnvironment(string environmentName)
    {
        return string.Equals(environmentName, "Production", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSmtpMode(EmailOptions options)
    {
        return string.Equals(options.Mode, EmailDeliveryModes.Smtp, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsResendMode(EmailOptions options)
    {
        return string.Equals(options.Mode, EmailDeliveryModes.Resend, StringComparison.OrdinalIgnoreCase);
    }
}
