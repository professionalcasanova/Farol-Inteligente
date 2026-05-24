namespace Farol.Infrastructure.Email;

public sealed record EmailSendResult(bool Succeeded, string? ProviderMessageId, string? ErrorCode)
{
    public static EmailSendResult Success(string? providerMessageId = null)
    {
        return new EmailSendResult(true, providerMessageId, null);
    }

    public static EmailSendResult Failure(string errorCode)
    {
        return new EmailSendResult(false, null, errorCode);
    }
}
