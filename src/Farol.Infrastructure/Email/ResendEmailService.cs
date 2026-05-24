using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Farol.Infrastructure.Email;

public sealed class ResendEmailService(
    HttpClient httpClient,
    IOptions<EmailOptions> options,
    ILogger<ResendEmailService> logger) : IEmailService
{
    public async Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var emailOptions = options.Value;

        if (string.IsNullOrWhiteSpace(emailOptions.Resend.ApiKey) ||
            string.IsNullOrWhiteSpace(emailOptions.FromAddress))
        {
            logger.LogWarning(
                "TransactionalEmailFailed Provider={Provider} ErrorCode={ErrorCode}",
                EmailDeliveryModes.Resend,
                EmailFailureCodes.ConfigurationError);

            return EmailSendResult.Failure(EmailFailureCodes.ConfigurationError);
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, emailOptions.Resend.ApiUrl)
            {
                Content = JsonContent.Create(new ResendSendEmailRequest(
                    FormatFromAddress(emailOptions),
                    [message.To.Address],
                    message.Subject,
                    message.HtmlBody,
                    message.TextBody))
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", emailOptions.Resend.ApiKey);

            using var response = await httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "TransactionalEmailFailed Provider={Provider} ErrorCode={ErrorCode} StatusCode={StatusCode}",
                    EmailDeliveryModes.Resend,
                    EmailFailureCodes.ProviderRejected,
                    (int)response.StatusCode);

                return EmailSendResult.Failure(EmailFailureCodes.ProviderRejected);
            }

            var providerResponse = await response.Content.ReadFromJsonAsync<ResendSendEmailResponse>(
                cancellationToken: cancellationToken);

            logger.LogInformation(
                "TransactionalEmailSent Provider={Provider} ProviderMessageId={ProviderMessageId}",
                EmailDeliveryModes.Resend,
                providerResponse?.Id ?? "unavailable");

            return EmailSendResult.Success(providerResponse?.Id);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            logger.LogWarning(
                "TransactionalEmailFailed Provider={Provider} ErrorCode={ErrorCode}",
                EmailDeliveryModes.Resend,
                EmailFailureCodes.DeliveryFailed);

            return EmailSendResult.Failure(EmailFailureCodes.DeliveryFailed);
        }
    }

    private static string FormatFromAddress(EmailOptions options)
    {
        return string.IsNullOrWhiteSpace(options.FromName)
            ? options.FromAddress
            : $"{options.FromName.Trim()} <{options.FromAddress.Trim()}>";
    }

    private sealed record ResendSendEmailRequest(
        [property: JsonPropertyName("from")] string From,
        [property: JsonPropertyName("to")] IReadOnlyList<string> To,
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("html")] string Html,
        [property: JsonPropertyName("text")] string Text);

    private sealed record ResendSendEmailResponse(
        [property: JsonPropertyName("id")] string? Id);
}
