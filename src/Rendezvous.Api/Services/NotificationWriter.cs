using Rendezvous.Domain.Notifications;
using Rendezvous.Infrastructure.Persistence;

namespace Rendezvous.Api.Services;

public class NotificationWriter
{
    private readonly AppDbContext dbContext;

    public NotificationWriter(AppDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public void Add(
        Guid userId,
        string title,
        string message,
        NotificationType type,
        string? linkUrl = null)
    {
        dbContext.Notifications.Add(new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            Type = type,
            LinkUrl = linkUrl,
            CreatedAtUtc = DateTime.UtcNow
        });
    }
}
