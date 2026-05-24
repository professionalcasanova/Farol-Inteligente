using System.Net;
using Microsoft.Extensions.Options;

namespace Farol.Infrastructure.Email;

public sealed class PasswordResetEmailTemplate(IOptions<EmailOptions> options)
{
    public EmailMessage Build(string recipientAddress, string? recipientName, string token)
    {
        var resetUrl = BuildPasswordResetUrl(token);
        var displayName = string.IsNullOrWhiteSpace(recipientName)
            ? "usuario"
            : recipientName.Trim();
        var encodedDisplayName = WebUtility.HtmlEncode(displayName);
        var encodedResetUrl = WebUtility.HtmlEncode(resetUrl);

        return new EmailMessage(
            new EmailRecipient(recipientAddress, recipientName),
            "Redefinicao de senha do Farol",
            $"""
            Ola, {displayName}.

            Recebemos uma solicitacao para redefinir sua senha no Farol.

            Use este link para criar uma nova senha:
            {resetUrl}

            Se voce nao solicitou essa alteracao, ignore este email.
            """,
            $"""
            <p>Ola, {encodedDisplayName}.</p>
            <p>Recebemos uma solicitacao para redefinir sua senha no Farol.</p>
            <p><a href="{encodedResetUrl}">Redefinir senha</a></p>
            <p>Se voce nao solicitou essa alteracao, ignore este email.</p>
            """);
    }

    private string BuildPasswordResetUrl(string token)
    {
        var baseUrl = options.Value.PublicBaseUrl.TrimEnd('/');
        var path = options.Value.PasswordResetPath.StartsWith("/", StringComparison.Ordinal)
            ? options.Value.PasswordResetPath
            : $"/{options.Value.PasswordResetPath}";

        return $"{baseUrl}{path}?token={Uri.EscapeDataString(token)}";
    }
}
