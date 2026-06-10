using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rendezvous.Api.Services;
using Rendezvous.Domain.Businesses;
using Rendezvous.Infrastructure.Persistence;

namespace Rendezvous.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/appointment-style-previews")]
public class AppointmentStylePreviewsController : ControllerBase
{
    private readonly AppDbContext dbContext;
    private readonly AppointmentStylePreviewStorageService storageService;

    public AppointmentStylePreviewsController(
        AppDbContext dbContext,
        AppointmentStylePreviewStorageService storageService)
    {
        this.dbContext = dbContext;
        this.storageService = storageService;
    }

    [HttpGet("{previewId:guid}/original")]
    public Task<IActionResult> GetOriginal(Guid previewId, CancellationToken cancellationToken)
    {
        return GetContentAsync(previewId, StylePreviewImageKind.Original, cancellationToken);
    }

    [HttpGet("{previewId:guid}/generated")]
    public Task<IActionResult> GetGenerated(Guid previewId, CancellationToken cancellationToken)
    {
        return GetContentAsync(previewId, StylePreviewImageKind.Generated, cancellationToken);
    }

    private async Task<IActionResult> GetContentAsync(
        Guid previewId,
        StylePreviewImageKind imageKind,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var preview = await dbContext.AppointmentStylePreviews
            .AsNoTracking()
            .Where(candidate => candidate.Id == previewId)
            .Select(candidate => new
            {
                candidate.AppointmentId,
                candidate.CustomerUserId,
                candidate.OriginalStorageKey,
                candidate.OriginalContentType,
                candidate.GeneratedStorageKey,
                candidate.GeneratedContentType
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (preview is null)
        {
            return NotFound();
        }

        var canView = preview.CustomerUserId == userId.Value
            || (preview.AppointmentId.HasValue
                && await IsAssignedEmployeeAsync(preview.AppointmentId.Value, userId.Value, cancellationToken));

        if (!canView)
        {
            return NotFound();
        }

        var storageKey = imageKind == StylePreviewImageKind.Original
            ? preview.OriginalStorageKey
            : preview.GeneratedStorageKey;
        var contentType = imageKind == StylePreviewImageKind.Original
            ? preview.OriginalContentType
            : preview.GeneratedContentType;

        var absolutePath = storageService.GetAbsolutePath(storageKey);
        if (!System.IO.File.Exists(absolutePath))
        {
            return NotFound();
        }

        return PhysicalFile(absolutePath, contentType);
    }

    private Task<bool> IsAssignedEmployeeAsync(
        Guid appointmentId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return dbContext.Appointments
            .AsNoTracking()
            .Where(appointment => appointment.Id == appointmentId)
            .Join(
                dbContext.StaffMembers.AsNoTracking().Where(staffMember =>
                    staffMember.UserId == userId
                    && staffMember.IsActive),
                appointment => appointment.StaffMemberId,
                staffMember => staffMember.Id,
                (appointment, _) => appointment)
            .Join(
                dbContext.BusinessMemberships.AsNoTracking().Where(membership =>
                    membership.UserId == userId
                    && membership.Role == BusinessMembershipRole.Employee
                    && membership.Status == BusinessMembershipStatus.Active),
                appointment => appointment.BusinessId,
                membership => membership.BusinessId,
                (appointment, _) => appointment)
            .AnyAsync(cancellationToken);
    }

    private Guid? GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(userIdValue, out var userId)
            ? userId
            : null;
    }

    private enum StylePreviewImageKind
    {
        Original,
        Generated
    }
}
