namespace Farol.Infrastructure.Email;

public sealed record EmailMessage(
    EmailRecipient To,
    string Subject,
    string TextBody,
    string HtmlBody);

public sealed record EmailRecipient(string Address, string? Name = null);
