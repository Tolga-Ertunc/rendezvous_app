using Microsoft.Extensions.Options;
using Resend;

namespace Rendezvous.Api.Email;

public class ResendEmailSender : IEmailSender
{
    private readonly IResend resend;
    private readonly EmailOptions options;

    public ResendEmailSender(IResend resend, IOptions<EmailOptions> options)
    {
        this.resend = resend;
        this.options = options.Value;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.FromAddress))
        {
            throw new InvalidOperationException("Email:FromAddress configuration is required.");
        }

        var from = string.IsNullOrWhiteSpace(options.FromName)
            ? options.FromAddress
            : $"{options.FromName} <{options.FromAddress}>";

        var resendMessage = new Resend.EmailMessage
        {
            From = from,
            Subject = message.Subject,
            TextBody = message.TextBody,
            HtmlBody = message.HtmlBody
        };
        resendMessage.To.Add(message.To);

        await resend.EmailSendAsync(resendMessage, cancellationToken);
    }
}
