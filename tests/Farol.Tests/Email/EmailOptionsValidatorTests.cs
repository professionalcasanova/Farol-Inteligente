using Farol.Infrastructure.Email;

namespace Farol.Tests.Email;

public sealed class EmailOptionsValidatorTests
{
    [Fact]
    public void EnsureSafeForEnvironment_ProductionWithoutFromAddress_ShouldThrow()
    {
        var options = ValidProductionOptions();
        options.FromAddress = string.Empty;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            EmailOptionsValidator.EnsureSafeForEnvironment(options, "Production"));

        Assert.Equal("Email:FromAddress configuration is required.", exception.Message);
    }

    [Fact]
    public void EnsureSafeForEnvironment_ProductionWithHttpPublicBaseUrl_ShouldThrow()
    {
        var options = ValidProductionOptions();
        options.PublicBaseUrl = "http://app.farol.com.br";

        var exception = Assert.Throws<InvalidOperationException>(() =>
            EmailOptionsValidator.EnsureSafeForEnvironment(options, "Production"));

        Assert.Equal("Email:PublicBaseUrl must use HTTPS in production.", exception.Message);
    }

    [Fact]
    public void EnsureSafeForEnvironment_ProductionWithResendWithoutApiKey_ShouldThrow()
    {
        var options = ValidProductionOptions();
        options.Resend.ApiKey = string.Empty;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            EmailOptionsValidator.EnsureSafeForEnvironment(options, "Production"));

        Assert.Equal("Email:Resend:ApiKey configuration is required when Email:Mode is Resend.", exception.Message);
    }

    [Fact]
    public void EnsureSafeForEnvironment_ProductionWithResendConfig_ShouldPass()
    {
        var options = ValidProductionOptions();

        EmailOptionsValidator.EnsureSafeForEnvironment(options, "Production");
    }

    [Fact]
    public void EnsureSafeForEnvironment_DevelopmentWithMailpitDefaults_ShouldPass()
    {
        var options = new EmailOptions();

        EmailOptionsValidator.EnsureSafeForEnvironment(options, "Development");
    }

    [Fact]
    public void EnsureSafeForEnvironment_StagingWithMailpitDefaults_ShouldPass()
    {
        var options = new EmailOptions();

        EmailOptionsValidator.EnsureSafeForEnvironment(options, "Staging");
    }

    private static EmailOptions ValidProductionOptions()
    {
        return new EmailOptions
        {
            Mode = EmailDeliveryModes.Resend,
            FromAddress = "no-reply@farol.com.br",
            FromName = "Farol",
            PublicBaseUrl = "https://app.farol.com.br",
            Resend = new ResendEmailOptions
            {
                ApiKey = "re_test_key"
            }
        };
    }
}
