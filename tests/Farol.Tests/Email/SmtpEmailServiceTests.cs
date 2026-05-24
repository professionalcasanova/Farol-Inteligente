using Farol.Infrastructure.Email;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Farol.Tests.Email;

public sealed class SmtpEmailServiceTests
{
    [Fact]
    public async Task SendAsync_MissingHost_ShouldReturnConfigurationError()
    {
        var service = new SmtpEmailService(
            Options.Create(new EmailOptions
            {
                FromAddress = "no-reply@farol.local",
                Smtp = new SmtpEmailOptions
                {
                    Host = string.Empty
                }
            }),
            NullLogger<SmtpEmailService>.Instance);

        var result = await service.SendAsync(
            new EmailMessage(
                new EmailRecipient("maria@email.com"),
                "Redefinicao de senha",
                "texto",
                "<p>texto</p>"),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(EmailFailureCodes.ConfigurationError, result.ErrorCode);
    }
}
