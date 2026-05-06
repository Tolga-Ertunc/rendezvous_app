using System.Collections.Concurrent;

namespace Rendezvous.Api.Email;

public class InMemoryEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> messages = new();

    public IReadOnlyCollection<EmailMessage> SentMessages => messages.ToList();

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        messages.Enqueue(message);

        return Task.CompletedTask;
    }
}
