using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Farol.Infrastructure.Email;

public sealed class SmtpEmailService(
    IOptions<EmailOptions> options,
    ILogger<SmtpEmailService> logger) : IEmailService
{
    public async Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var emailOptions = options.Value;

        if (string.IsNullOrWhiteSpace(emailOptions.Smtp.Host) ||
            string.IsNullOrWhiteSpace(emailOptions.FromAddress))
        {
            logger.LogWarning(
                "TransactionalEmailFailed Provider={Provider} ErrorCode={ErrorCode}",
                EmailDeliveryModes.Smtp,
                EmailFailureCodes.ConfigurationError);

            return EmailSendResult.Failure(EmailFailureCodes.ConfigurationError);
        }

        try
        {
            using var smtpClient = new SmtpClient(emailOptions.Smtp.Host, emailOptions.Smtp.Port)
            {
                EnableSsl = emailOptions.Smtp.UseTls
            };

            if (!string.IsNullOrWhiteSpace(emailOptions.Smtp.Username))
            {
                smtpClient.Credentials = new NetworkCredential(
                    emailOptions.Smtp.Username,
                    emailOptions.Smtp.Password);
            }

            using var mailMessage = new MailMessage
            {
                From = new MailAddress(emailOptions.FromAddress, emailOptions.FromName),
                Subject = message.Subject,
                Body = message.HtmlBody,
                IsBodyHtml = true
            };
            mailMessage.To.Add(new MailAddress(message.To.Address, message.To.Name));

            if (!string.IsNullOrWhiteSpace(message.TextBody))
            {
                mailMessage.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
                    message.TextBody,
                    null,
                    "text/plain"));
            }

            await smtpClient.SendMailAsync(mailMessage).WaitAsync(cancellationToken);

            logger.LogInformation(
                "TransactionalEmailSent Provider={Provider}",
                EmailDeliveryModes.Smtp);

            return EmailSendResult.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            logger.LogWarning(
                "TransactionalEmailFailed Provider={Provider} ErrorCode={ErrorCode}",
                EmailDeliveryModes.Smtp,
                EmailFailureCodes.DeliveryFailed);

            return EmailSendResult.Failure(EmailFailureCodes.DeliveryFailed);
        }
    }
}
