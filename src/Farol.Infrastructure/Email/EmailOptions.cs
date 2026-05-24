namespace Farol.Infrastructure.Email;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string Mode { get; set; } = EmailDeliveryModes.Smtp;
    public string FromAddress { get; set; } = "no-reply@farol.local";
    public string FromName { get; set; } = "Farol";
    public string PublicBaseUrl { get; set; } = "http://localhost:3000";
    public string PasswordResetPath { get; set; } = "/reset-password";
    public int ResetTokenMinutes { get; set; } = 60;
    public SmtpEmailOptions Smtp { get; set; } = new();
    public ResendEmailOptions Resend { get; set; } = new();
}

public sealed class SmtpEmailOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1025;
    public bool UseTls { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class ResendEmailOptions
{
    public string ApiUrl { get; set; } = "https://api.resend.com/emails";
    public string ApiKey { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 10;
}
