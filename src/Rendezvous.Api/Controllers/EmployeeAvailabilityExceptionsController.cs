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
[Route("api/employee/availability-exceptions")]
public class EmployeeAvailabilityExceptionsController : ControllerBase
{
    private readonly AppDbContext dbContext;
    private readonly AvailabilityExceptionService availabilityExceptionService;

    public EmployeeAvailabilityExceptionsController(
        AppDbContext dbContext,
        AvailabilityExceptionService availabilityExceptionService)
    {
        this.dbContext = dbContext;
        this.availabilityExceptionService = availabilityExceptionService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AvailabilityExceptionResponse>>> List(
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        return await GetEmployeeExceptionsQuery(userId.Value)
            .ToListAsync(cancellationToken);
    }

    [HttpPost]
    public async Task<ActionResult<AvailabilityExceptionResponse>> Create(
        AvailabilityExceptionRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var validation = await ValidateRequestAsync(userId.Value, request, null, cancellationToken);
        var validationResult = validation?.Result;
        if (validationResult is not null)
        {
            return validationResult;
        }

        var validationValue = validation!.Value!;
        var draft = validationValue.Draft;
        if (await availabilityExceptionService.HasOverlappingExceptionAsync(draft, null, cancellationToken))
        {
            return Conflict(new { message = "The exception overlaps an existing exception." });
        }

        var conflicts = await availabilityExceptionService.GetConflictingAppointmentsAsync(
            draft,
            validationValue.BusinessTimeZoneId,
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
                validationValue.BusinessTimeZoneId,
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
            $"/api/employee/availability-exceptions/{exception.Id}",
            await GetEmployeeExceptionByIdAsync(userId.Value, exception.Id, cancellationToken));
    }

    [HttpPut("{exceptionId:guid}")]
    public async Task<ActionResult<AvailabilityExceptionResponse>> Update(
        Guid exceptionId,
        AvailabilityExceptionRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var exception = await GetOwnedExceptionAsync(userId.Value, exceptionId, cancellationToken);
        if (exception is null)
        {
            return NotFound();
        }

        var validation = await ValidateRequestAsync(userId.Value, request, exception.BusinessId, cancellationToken);
        var validationResult = validation?.Result;
        if (validationResult is not null)
        {
            return validationResult;
        }

        var validationValue = validation!.Value!;
        var draft = validationValue.Draft;
        if (await availabilityExceptionService.HasOverlappingExceptionAsync(draft, exceptionId, cancellationToken))
        {
            return Conflict(new { message = "The exception overlaps an existing exception." });
        }

        var conflicts = await availabilityExceptionService.GetConflictingAppointmentsAsync(
            draft,
            validationValue.BusinessTimeZoneId,
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
                validationValue.BusinessTimeZoneId,
                DateTimeOffset.UtcNow,
                cancellationToken);
        }

        exception.BusinessId = draft.BusinessId;
        exception.StaffMemberId = draft.StaffMemberId;
        exception.Type = draft.Type;
        exception.Date = draft.Date;
        exception.IsFullDay = draft.IsFullDay;
        exception.StartsAt = draft.StartsAt;
        exception.EndsAt = draft.EndsAt;
        exception.Note = validationValue.Note;

        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetEmployeeExceptionByIdAsync(userId.Value, exception.Id, cancellationToken);
    }

    [HttpDelete("{exceptionId:guid}")]
    public async Task<IActionResult> Delete(
        Guid exceptionId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var exception = await GetOwnedExceptionAsync(userId.Value, exceptionId, cancellationToken);
        if (exception is null)
        {
            return NotFound();
        }

        dbContext.AvailabilityExceptions.Remove(exception);
        await dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private IQueryable<AvailabilityExceptionResponse> GetEmployeeExceptionsQuery(Guid userId)
    {
        return dbContext.AvailabilityExceptions
            .AsNoTracking()
            .Where(exception => exception.Type == AvailabilityExceptionType.StaffLeave)
            .Join(
                dbContext.StaffMembers.AsNoTracking(),
                exception => exception.StaffMemberId,
                staffMember => staffMember.Id,
                (exception, staffMember) => new { exception, staffMember })
            .Where(row =>
                row.staffMember.UserId == userId
                && row.staffMember.IsActive
                && dbContext.BusinessMemberships.Any(membership =>
                    membership.BusinessId == row.exception.BusinessId
                    && membership.UserId == userId
                    && membership.Role == BusinessMembershipRole.Employee
                    && membership.Status == BusinessMembershipStatus.Active))
            .Join(
                dbContext.Users.AsNoTracking(),
                row => row.staffMember.UserId,
                user => user.Id,
                (row, staffUser) => new { row.exception, row.staffMember, staffUser })
            .OrderBy(row => row.exception.Date)
            .ThenBy(row => row.exception.StartsAt)
            .Select(row => new AvailabilityExceptionResponse(
                row.exception.Id,
                row.exception.BusinessId,
                row.exception.StaffMemberId,
                ((row.staffUser.FirstName ?? string.Empty) + " " + (row.staffUser.LastName ?? string.Empty)).Trim(),
                row.exception.Type.ToString(),
                row.exception.Date,
                row.exception.IsFullDay,
                row.exception.StartsAt == null ? null : row.exception.StartsAt.Value.ToString("HH:mm"),
                row.exception.EndsAt == null ? null : row.exception.EndsAt.Value.ToString("HH:mm"),
                row.exception.Note,
                row.exception.CreatedAtUtc));
    }

    private Task<AvailabilityExceptionResponse> GetEmployeeExceptionByIdAsync(
        Guid userId,
        Guid exceptionId,
        CancellationToken cancellationToken)
    {
        return GetEmployeeExceptionsQuery(userId)
            .SingleAsync(exception => exception.Id == exceptionId, cancellationToken);
    }

    private Task<AvailabilityException?> GetOwnedExceptionAsync(
        Guid userId,
        Guid exceptionId,
        CancellationToken cancellationToken)
    {
        return dbContext.AvailabilityExceptions
            .Where(exception =>
                exception.Id == exceptionId
                && exception.Type == AvailabilityExceptionType.StaffLeave)
            .Join(
                dbContext.StaffMembers,
                exception => exception.StaffMemberId,
                staffMember => staffMember.Id,
                (exception, staffMember) => new { exception, staffMember })
            .Where(row =>
                row.staffMember.UserId == userId
                && row.staffMember.IsActive
                && dbContext.BusinessMemberships.Any(membership =>
                    membership.BusinessId == row.exception.BusinessId
                    && membership.UserId == userId
                    && membership.Role == BusinessMembershipRole.Employee
                    && membership.Status == BusinessMembershipStatus.Active))
            .Select(row => row.exception)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<ActionResult<EmployeeAvailabilityExceptionValidation>?> ValidateRequestAsync(
        Guid userId,
        AvailabilityExceptionRequest request,
        Guid? existingBusinessId,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<AvailabilityExceptionType>(request.Type, ignoreCase: true, out var type)
            || type != AvailabilityExceptionType.StaffLeave)
        {
            return BadRequest(new { message = "Employee availability exceptions must be staff leave." });
        }

        if (!ValidateTimeRange(request, out var startsAt, out var endsAt, out var timeError))
        {
            return BadRequest(new { message = timeError });
        }

        var businessId = request.BusinessId ?? existingBusinessId;
        if (!businessId.HasValue)
        {
            return BadRequest(new { message = "businessId is required." });
        }

        if (existingBusinessId.HasValue && businessId.Value != existingBusinessId.Value)
        {
            return BadRequest(new { message = "Business cannot be changed for an existing leave record." });
        }

        var staffQuery = dbContext.StaffMembers
            .AsNoTracking()
            .Where(staffMember =>
                staffMember.BusinessId == businessId.Value
                && staffMember.UserId == userId
                && staffMember.IsActive
                && dbContext.BusinessMemberships.Any(membership =>
                    membership.BusinessId == staffMember.BusinessId
                    && membership.UserId == userId
                    && membership.Role == BusinessMembershipRole.Employee
                    && membership.Status == BusinessMembershipStatus.Active));

        if (request.StaffMemberId.HasValue)
        {
            staffQuery = staffQuery.Where(staffMember => staffMember.Id == request.StaffMemberId.Value);
        }

        var staff = await staffQuery
            .Join(
                dbContext.Businesses.AsNoTracking(),
                staffMember => staffMember.BusinessId,
                business => business.Id,
                (staffMember, business) => new
                {
                    staffMember.Id,
                    business.TimeZoneId
                })
            .OrderBy(staffMember => staffMember.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (staff is null)
        {
            return NotFound();
        }

        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        if (note?.Length > 500)
        {
            return BadRequest(new { message = "Note cannot exceed 500 characters." });
        }

        var draft = new AvailabilityExceptionDraft(
            businessId.Value,
            staff.Id,
            AvailabilityExceptionType.StaffLeave,
            request.Date,
            request.IsFullDay,
            startsAt,
            endsAt);

        return new EmployeeAvailabilityExceptionValidation(draft, staff.TimeZoneId, note);
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

    private sealed record EmployeeAvailabilityExceptionValidation(
        AvailabilityExceptionDraft Draft,
        string BusinessTimeZoneId,
        string? Note);
}
