using Farol.Infrastructure.Email;
using Microsoft.Extensions.Options;

namespace Farol.Tests.Email;

public sealed class PasswordResetEmailTemplateTests
{
    [Fact]
    public void Build_ConfiguredFrontendUrl_ShouldCreatePasswordResetLink()
    {
        const string token = "RESET TOKEN/123";
        var template = new PasswordResetEmailTemplate(Options.Create(new EmailOptions
        {
            PublicBaseUrl = "https://app.farol.com.br/",
            PasswordResetPath = "recuperar-senha"
        }));

        var message = template.Build("maria@email.com", "Maria Silva", token);

        Assert.Equal("maria@email.com", message.To.Address);
        Assert.Equal("Maria Silva", message.To.Name);
        Assert.Equal("Redefinicao de senha do Farol", message.Subject);
        Assert.Contains(
            "https://app.farol.com.br/recuperar-senha?token=RESET%20TOKEN%2F123",
            message.TextBody,
            StringComparison.Ordinal);
        Assert.Contains("Redefinir senha", message.HtmlBody, StringComparison.Ordinal);
    }
}
