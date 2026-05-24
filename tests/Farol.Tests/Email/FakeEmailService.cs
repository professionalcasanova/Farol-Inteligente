using Farol.Infrastructure.Email;

namespace Farol.Tests.Email;

public sealed class FakeEmailService : IEmailService
{
    public List<EmailMessage> Messages { get; } = [];
    public EmailSendResult NextResult { get; set; } = EmailSendResult.Success("test-email-message-id");

    public Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        Messages.Add(message);

        return Task.FromResult(NextResult);
    }

    public void Reset()
    {
        Messages.Clear();
        NextResult = EmailSendResult.Success("test-email-message-id");
    }
}
