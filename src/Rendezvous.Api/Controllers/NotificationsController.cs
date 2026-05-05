using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rendezvous.Infrastructure.Persistence;

namespace Rendezvous.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly AppDbContext dbContext;

    public NotificationsController(AppDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<NotificationsResponse>> List(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var notifications = await dbContext.Notifications
            .AsNoTracking()
            .Where(notification => notification.UserId == userId.Value)
            .OrderByDescending(notification => notification.CreatedAtUtc)
            .Take(30)
            .Select(notification => new NotificationResponse(
                notification.Id,
                notification.Title,
                notification.Message,
                notification.LinkUrl,
                notification.Type.ToString(),
                notification.CreatedAtUtc,
                notification.ReadAtUtc))
            .ToListAsync(cancellationToken);

        var unreadCount = await dbContext.Notifications
            .AsNoTracking()
            .CountAsync(
                notification =>
                    notification.UserId == userId.Value
                    && notification.ReadAtUtc == null,
                cancellationToken);

        return new NotificationsResponse(unreadCount, notifications);
    }

    [HttpPost("{notificationId:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid notificationId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var notification = await dbContext.Notifications
            .SingleOrDefaultAsync(
                candidate => candidate.Id == notificationId && candidate.UserId == userId.Value,
                cancellationToken);

        if (notification is null)
        {
            return NoContent();
        }

        notification.ReadAtUtc ??= DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var nowUtc = DateTime.UtcNow;
        await dbContext.Notifications
            .Where(notification =>
                notification.UserId == userId.Value
                && notification.ReadAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(notification => notification.ReadAtUtc, nowUtc),
                cancellationToken);

        return NoContent();
    }

    private Guid? GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(userIdValue, out var userId)
            ? userId
            : null;
    }
}

public sealed record NotificationsResponse(
    int UnreadCount,
    IReadOnlyList<NotificationResponse> Notifications);

public sealed record NotificationResponse(
    Guid Id,
    string Title,
    string Message,
    string? LinkUrl,
    string Type,
    DateTime CreatedAtUtc,
    DateTime? ReadAtUtc);
