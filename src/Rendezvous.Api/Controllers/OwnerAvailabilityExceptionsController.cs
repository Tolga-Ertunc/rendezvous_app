using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rendezvous.Api.Services;
using Rendezvous.Domain.Availability;
using Rendezvous.Domain.Businesses;
using Rendezvous.Infrastructure.Persistence;

namespace Rendezvous.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/owner/businesses/{businessId:guid}/availability-exceptions")]
public class OwnerAvailabilityExceptionsController : ControllerBase
{
    private readonly AppDbContext dbContext;
    private readonly AvailabilityExceptionService availabilityExceptionService;

    public OwnerAvailabilityExceptionsController(
        AppDbContext dbContext,
        AvailabilityExceptionService availabilityExceptionService)
    {
        this.dbContext = dbContext;
        this.availabilityExceptionService = availabilityExceptionService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AvailabilityExceptionResponse>>> List(
        Guid businessId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        if (!await HasActiveOwnerMembershipAsync(businessId, userId.Value, cancellationToken))
        {
            return NotFound();
        }

        return await GetBusinessExceptionsQuery(businessId)
            .ToListAsync(cancellationToken);
    }

    [HttpPost]
    public async Task<ActionResult<AvailabilityExceptionResponse>> Create(
        Guid businessId,
        AvailabilityExceptionRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        if (!await HasActiveOwnerMembershipAsync(businessId, userId.Value, cancellationToken))
        {
            return NotFound();
        }

        var validation = await ValidateRequestAsync(businessId, request, cancellationToken);
        var validationResult = validation?.Result;
        if (validationResult is not null)
        {
            return validationResult;
        }

        var validationValue = validation!.Value!;
        var draft = validationValue.Draft;
        var timeZoneId = validationValue.BusinessTimeZoneId;
        if (await availabilityExceptionService.HasOverlappingExceptionAsync(draft, null, cancellationToken))
        {
            return Conflict(new { message = "The exception overlaps an existing exception." });
        }

        var conflicts = await availabilityExceptionService.GetConflictingAppointmentsAsync(
            draft,
            timeZoneId,
            DateTimeOffset.UtcNow,
            cancellationToken);
        if (conflicts.Count > 0 && !request.CancelConflictingAppointments)
        {
            return Conflict(CreateConflictResponse(conflicts));
        }

        if (conflicts.Count > 0)
        {
            await availabilityExceptionService.CancelConflictingAppointmentsAsync(
                draft,
                timeZoneId,
                DateTimeOffset.UtcNow,
                cancellationToken);
        }

        var exception = new AvailabilityException
        {
            BusinessId = draft.BusinessId,
            StaffMemberId = draft.StaffMemberId,
            Type = draft.Type,
            Date = draft.Date,
            IsFullDay = draft.IsFullDay,
            StartsAt = draft.StartsAt,
            EndsAt = draft.EndsAt,
            Note = validationValue.Note,
            CreatedByUserId = userId.Value,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.AvailabilityExceptions.Add(exception);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Created(
            $"/api/owner/businesses/{businessId}/availability-exceptions/{exception.Id}",
            await GetBusinessExceptionByIdAsync(businessId, exception.Id, cancellationToken));
    }

    [HttpPut("{exceptionId:guid}")]
    public async Task<ActionResult<AvailabilityExceptionResponse>> Update(
        Guid businessId,
        Guid exceptionId,
        AvailabilityExceptionRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        if (!await HasActiveOwnerMembershipAsync(businessId, userId.Value, cancellationToken))
        {
            return NotFound();
        }

        var exception = await dbContext.AvailabilityExceptions
            .SingleOrDefaultAsync(
                candidate => candidate.Id == exceptionId && candidate.BusinessId == businessId,
                cancellationToken);
        if (exception is null)
        {
            return NotFound();
        }

        var validation = await ValidateRequestAsync(businessId, request, cancellationToken);
        var validationResult = validation?.Result;
        if (validationResult is not null)
        {
            return validationResult;
        }

        var validationValue = validation!.Value!;
        var draft = validationValue.Draft;
        var timeZoneId = validationValue.BusinessTimeZoneId;
        if (await availabilityExceptionService.HasOverlappingExceptionAsync(draft, exceptionId, cancellationToken))
        {
            return Conflict(new { message = "The exception overlaps an existing exception." });
        }

        var conflicts = await availabilityExceptionService.GetConflictingAppointmentsAsync(
            draft,
            timeZoneId,
            DateTimeOffset.UtcNow,
            cancellationToken);
        if (conflicts.Count > 0 && !request.CancelConflictingAppointments)
        {
            return Conflict(CreateConflictResponse(conflicts));
        }

        if (conflicts.Count > 0)
        {
            await availabilityExceptionService.CancelConflictingAppointmentsAsync(
                draft,
                timeZoneId,
                DateTimeOffset.UtcNow,
                cancellationToken);
        }

        exception.StaffMemberId = draft.StaffMemberId;
        exception.Type = draft.Type;
        exception.Date = draft.Date;
        exception.IsFullDay = draft.IsFullDay;
        exception.StartsAt = draft.StartsAt;
        exception.EndsAt = draft.EndsAt;
        exception.Note = validationValue.Note;

        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetBusinessExceptionByIdAsync(businessId, exception.Id, cancellationToken);
    }

    [HttpDelete("{exceptionId:guid}")]
    public async Task<IActionResult> Delete(
        Guid businessId,
        Guid exceptionId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        if (!await HasActiveOwnerMembershipAsync(businessId, userId.Value, cancellationToken))
        {
            return NotFound();
        }

        var exception = await dbContext.AvailabilityExceptions
            .SingleOrDefaultAsync(
                candidate => candidate.Id == exceptionId && candidate.BusinessId == businessId,
                cancellationToken);
        if (exception is null)
        {
            return NotFound();
        }

        dbContext.AvailabilityExceptions.Remove(exception);
        await dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private async Task<AvailabilityExceptionResponse> GetBusinessExceptionByIdAsync(
        Guid businessId,
        Guid exceptionId,
        CancellationToken cancellationToken)
    {
        var response = await GetBusinessExceptionsQuery(businessId)
            .SingleAsync(exception => exception.Id == exceptionId, cancellationToken);

        return response;
    }

    private IQueryable<AvailabilityExceptionResponse> GetBusinessExceptionsQuery(Guid businessId)
    {
        return dbContext.AvailabilityExceptions
            .AsNoTracking()
            .Where(exception => exception.BusinessId == businessId)
            .OrderBy(exception => exception.Date)
            .ThenBy(exception => exception.StartsAt)
            .ThenBy(exception => exception.Type)
            .GroupJoin(
                dbContext.StaffMembers.AsNoTracking(),
                exception => exception.StaffMemberId,
                staffMember => staffMember.Id,
                (exception, staffMembers) => new { exception, staffMembers })
            .SelectMany(
                row => row.staffMembers.DefaultIfEmpty(),
                (row, staffMember) => new AvailabilityExceptionResponse(
                    row.exception.Id,
                    row.exception.BusinessId,
                    row.exception.StaffMemberId,
                    staffMember == null ? null : staffMember.DisplayName,
                    row.exception.Type.ToString(),
                    row.exception.Date,
                    row.exception.IsFullDay,
                    row.exception.StartsAt == null ? null : row.exception.StartsAt.Value.ToString("HH:mm"),
                    row.exception.EndsAt == null ? null : row.exception.EndsAt.Value.ToString("HH:mm"),
                    row.exception.Note,
                    row.exception.CreatedAtUtc));
    }

    private async Task<ActionResult<OwnerAvailabilityExceptionValidation>?> ValidateRequestAsync(
        Guid businessId,
        AvailabilityExceptionRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<AvailabilityExceptionType>(request.Type, ignoreCase: true, out var type))
        {
            return BadRequest(new { message = "Invalid exception type." });
        }

        if (!ValidateTimeRange(request, out var startsAt, out var endsAt, out var timeError))
        {
            return BadRequest(new { message = timeError });
        }

        Guid? staffMemberId = null;
        if (type == AvailabilityExceptionType.StaffLeave)
        {
            if (!request.StaffMemberId.HasValue)
            {
                return BadRequest(new { message = "Staff leave requires staffMemberId." });
            }

            var staffExists = await dbContext.StaffMembers
                .AsNoTracking()
                .AnyAsync(
                    staffMember =>
                        staffMember.Id == request.StaffMemberId.Value
                        && staffMember.BusinessId == businessId,
                    cancellationToken);
            if (!staffExists)
            {
                return NotFound();
            }

            staffMemberId = request.StaffMemberId.Value;
        }
        else if (request.StaffMemberId.HasValue)
        {
            return BadRequest(new { message = "Business-level exceptions cannot include staffMemberId." });
        }

        var businessTimeZoneId = await dbContext.Businesses
            .AsNoTracking()
            .Where(business => business.Id == businessId)
            .Select(business => business.TimeZoneId)
            .SingleAsync(cancellationToken);
        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        if (note?.Length > 500)
        {
            return BadRequest(new { message = "Note cannot exceed 500 characters." });
        }

        var draft = new AvailabilityExceptionDraft(
            businessId,
            staffMemberId,
            type,
            request.Date,
            request.IsFullDay,
            startsAt,
            endsAt);

        return new OwnerAvailabilityExceptionValidation(draft, businessTimeZoneId, note);
    }

    private static bool ValidateTimeRange(
        AvailabilityExceptionRequest request,
        out TimeOnly? startsAt,
        out TimeOnly? endsAt,
        out string? error)
    {
        startsAt = null;
        endsAt = null;
        error = null;

        if (request.IsFullDay)
        {
            return true;
        }

        if (!TimeOnly.TryParse(request.StartsAt, out var parsedStartsAt)
            || !TimeOnly.TryParse(request.EndsAt, out var parsedEndsAt))
        {
            error = "Partial exceptions require valid startsAt and endsAt values.";
            return false;
        }

        if (parsedStartsAt >= parsedEndsAt)
        {
            error = "startsAt must be earlier than endsAt.";
            return false;
        }

        startsAt = parsedStartsAt;
        endsAt = parsedEndsAt;
        return true;
    }

    private Task<bool> HasActiveOwnerMembershipAsync(
        Guid businessId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return dbContext.BusinessMemberships
            .AsNoTracking()
            .AnyAsync(
                membership =>
                    membership.BusinessId == businessId
                    && membership.UserId == userId
                    && membership.Role == BusinessMembershipRole.Owner
                    && membership.Status == BusinessMembershipStatus.Active,
                cancellationToken);
    }

    private static AvailabilityExceptionConflictResponse CreateConflictResponse(
        IReadOnlyList<AvailabilityExceptionAppointmentConflict> conflicts)
    {
        return new AvailabilityExceptionConflictResponse(
            "The selected closure/leave overlaps active appointments.",
            conflicts.Count,
            conflicts
                .Select(conflict => new AvailabilityExceptionAppointmentResponse(
                    conflict.Id,
                    conflict.Status,
                    conflict.StartsAtUtc,
                    conflict.EndsAtUtc,
                    conflict.ServiceName,
                    conflict.StaffDisplayName))
                .ToList());
    }

    private Guid? GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(userIdValue, out var userId)
            ? userId
            : null;
    }

    private sealed record OwnerAvailabilityExceptionValidation(
        AvailabilityExceptionDraft Draft,
        string BusinessTimeZoneId,
        string? Note);
}
