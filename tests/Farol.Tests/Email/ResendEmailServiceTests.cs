using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Farol.Infrastructure.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Farol.Tests.Email;

public sealed class ResendEmailServiceTests
{
    [Fact]
    public async Task SendAsync_ProviderAcceptsMessage_ShouldPostPayloadAndReturnProviderMessageId()
    {
        const string apiKey = "re_secret_key";
        const string resetToken = "RESET_TOKEN_123";
        var handler = new CapturingHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"id\":\"email_123\"}", Encoding.UTF8, "application/json")
        });
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.resend.com")
        };
        var logger = new CapturingLogger<ResendEmailService>();
        var service = new ResendEmailService(
            httpClient,
            Options.Create(new EmailOptions
            {
                FromAddress = "no-reply@farol.com.br",
                FromName = "Farol",
                Resend = new ResendEmailOptions
                {
                    ApiKey = apiKey
                }
            }),
            logger);

        var result = await service.SendAsync(
            new EmailMessage(
                new EmailRecipient("maria@email.com", "Maria Silva"),
                "Redefinicao de senha",
                $"Use este link com token {resetToken}.",
                $"<p>Use este link com token {resetToken}.</p>"),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("email_123", result.ProviderMessageId);
        Assert.Equal(HttpMethod.Post, handler.Request?.Method);
        Assert.Equal("/emails", handler.Request?.RequestUri?.PathAndQuery);
        Assert.Equal(new AuthenticationHeaderValue("Bearer", apiKey), handler.Request?.Headers.Authorization);
        using var requestJson = JsonDocument.Parse(handler.RequestBody);
        Assert.Equal(
            "Farol <no-reply@farol.com.br>",
            requestJson.RootElement.GetProperty("from").GetString());
        Assert.Equal(
            "maria@email.com",
            requestJson.RootElement.GetProperty("to").EnumerateArray().Single().GetString());
        Assert.Contains(resetToken, handler.RequestBody, StringComparison.Ordinal);

        var loggedContent = string.Join(Environment.NewLine, logger.Messages);

        Assert.Contains("TransactionalEmailSent", loggedContent, StringComparison.Ordinal);
        Assert.Contains("Resend", loggedContent, StringComparison.Ordinal);
        Assert.Contains("email_123", loggedContent, StringComparison.Ordinal);
        Assert.DoesNotContain(resetToken, loggedContent, StringComparison.Ordinal);
        Assert.DoesNotContain(apiKey, loggedContent, StringComparison.Ordinal);
        Assert.DoesNotContain("maria@email.com", loggedContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendAsync_ProviderRejectsMessage_ShouldReturnFailureWithoutSensitiveLogs()
    {
        const string resetToken = "RESET_TOKEN_123";
        var handler = new CapturingHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"message\":\"bad request\"}", Encoding.UTF8, "application/json")
        });
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.resend.com")
        };
        var logger = new CapturingLogger<ResendEmailService>();
        var service = new ResendEmailService(
            httpClient,
            Options.Create(new EmailOptions
            {
                FromAddress = "no-reply@farol.com.br",
                FromName = "Farol",
                Resend = new ResendEmailOptions
                {
                    ApiKey = "re_secret_key"
                }
            }),
            logger);

        var result = await service.SendAsync(
            new EmailMessage(
                new EmailRecipient("maria@email.com"),
                "Redefinicao de senha",
                resetToken,
                $"<p>{resetToken}</p>"),
            CancellationToken.None);

        var loggedContent = string.Join(Environment.NewLine, logger.Messages);

        Assert.False(result.Succeeded);
        Assert.Equal(EmailFailureCodes.ProviderRejected, result.ErrorCode);
        Assert.Contains("TransactionalEmailFailed", loggedContent, StringComparison.Ordinal);
        Assert.DoesNotContain(resetToken, loggedContent, StringComparison.Ordinal);
        Assert.DoesNotContain("maria@email.com", loggedContent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bad request", loggedContent, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class CapturingHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return response;
        }
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
