namespace Rendezvous.Api.Email;

public class DisabledEmailSender : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("Email sending is disabled. Configure Email:Provider and provider credentials.");
    }
}
