namespace WeeklySalesCoach.Services.Notifications;

public record EmailMessage(string To, string Subject, string Body);

public record EmailSendResult(bool Sent, string? Error);

/// <summary>
/// Delivery provider for outgoing email. The demo ships with <see cref="NoProviderEmailSender"/>;
/// plugging in SMTP, SendGrid, etc. means implementing this interface and registering it in Program.cs.
/// </summary>
public interface IEmailSender
{
    string ProviderName { get; }
    bool IsConfigured { get; }
    Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken ct = default);
}

/// <summary>Default: nothing is sent; queued emails wait in the outbox until a provider is configured.</summary>
public sealed class NoProviderEmailSender : IEmailSender
{
    public string ProviderName => "None";
    public bool IsConfigured => false;

    public Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken ct = default) =>
        Task.FromResult(new EmailSendResult(false, "No email provider configured."));
}
