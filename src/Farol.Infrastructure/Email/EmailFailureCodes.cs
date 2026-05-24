namespace Farol.Infrastructure.Email;

public static class EmailFailureCodes
{
    public const string ConfigurationError = "email_configuration_error";
    public const string DeliveryFailed = "email_delivery_failed";
    public const string ProviderRejected = "email_provider_rejected";
}
