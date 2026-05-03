using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rendezvous.Domain.Availability;
using Rendezvous.Domain.Businesses;
using Rendezvous.Infrastructure.Persistence;

namespace Rendezvous.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/owner/businesses/{businessId:guid}/staff/{staffMemberId:guid}/working-hours")]
public class OwnerStaffWorkingHoursController : ControllerBase
{
    private readonly AppDbContext dbContext;

    public OwnerStaffWorkingHoursController(AppDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OwnerWorkingHourResponse>>> Get(
        Guid businessId,
        Guid staffMemberId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        if (!await HasActiveOwnerMembershipAsync(businessId, userId.Value, cancellationToken)
            || !await dbContext.StaffMembers.AsNoTracking().AnyAsync(
                staffMember => staffMember.Id == staffMemberId && staffMember.BusinessId == businessId,
                cancellationToken))
        {
            return NotFound();
        }

        var rows = await dbContext.StaffWorkingHours
            .AsNoTracking()
            .Where(workingHour => workingHour.StaffMemberId == staffMemberId)
            .OrderBy(workingHour => workingHour.DayOfWeek)
            .Select(workingHour => new OwnerWorkingHourResponse(
                (int)workingHour.DayOfWeek,
                false,
                workingHour.StartsAt.ToString("HH:mm"),
                workingHour.EndsAt.ToString("HH:mm")))
            .ToListAsync(cancellationToken);

        return Ok(FillClosedDays(rows));
    }

    [HttpPut]
    public async Task<ActionResult<IReadOnlyList<OwnerWorkingHourResponse>>> Update(
        Guid businessId,
        Guid staffMemberId,
        IReadOnlyList<OwnerWorkingHourRequest> request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        if (!await HasActiveOwnerMembershipAsync(businessId, userId.Value, cancellationToken)
            || !await dbContext.StaffMembers.AsNoTracking().AnyAsync(
                staffMember => staffMember.Id == staffMemberId && staffMember.BusinessId == businessId,
                cancellationToken))
        {
            return NotFound();
        }

        var validationError = ValidateWorkingHours(request);
        if (validationError is not null)
        {
            return BadRequest(new { message = validationError });
        }

        var existingRows = await dbContext.StaffWorkingHours
            .Where(workingHour => workingHour.StaffMemberId == staffMemberId)
            .ToListAsync(cancellationToken);

        foreach (var day in Enumerable.Range(0, 7))
        {
            var requestedDay = request.SingleOrDefault(candidate => candidate.DayOfWeek == day);
            var existingRow = existingRows.SingleOrDefault(candidate => (int)candidate.DayOfWeek == day);

            if (requestedDay is null || requestedDay.IsClosed)
            {
                if (existingRow is not null)
                {
                    dbContext.StaffWorkingHours.Remove(existingRow);
                }

                continue;
            }

            var startsAt = TimeOnly.Parse(requestedDay.OpensAt!);
            var endsAt = TimeOnly.Parse(requestedDay.ClosesAt!);

            if (existingRow is null)
            {
                dbContext.StaffWorkingHours.Add(new StaffWorkingHour
                {
                    StaffMemberId = staffMemberId,
                    DayOfWeek = (DayOfWeek)day,
                    StartsAt = startsAt,
                    EndsAt = endsAt
                });

                continue;
            }

            existingRow.StartsAt = startsAt;
            existingRow.EndsAt = endsAt;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return await Get(businessId, staffMemberId, cancellationToken);
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

    private static string? ValidateWorkingHours(IReadOnlyList<OwnerWorkingHourRequest> request)
    {
        if (request.Count > 7 || request.Select(day => day.DayOfWeek).Distinct().Count() != request.Count)
        {
            return "Working hours must contain at most one row per day.";
        }

        foreach (var day in request)
        {
            if (day.DayOfWeek < 0 || day.DayOfWeek > 6)
            {
                return "Day of week must be between 0 and 6.";
            }

            if (day.IsClosed)
            {
                continue;
            }

            if (!TimeOnly.TryParse(day.OpensAt, out var startsAt)
                || !TimeOnly.TryParse(day.ClosesAt, out var endsAt))
            {
                return "Open days must include valid startsAt and endsAt values.";
            }

            if (startsAt >= endsAt)
            {
                return "startsAt must be earlier than endsAt.";
            }
        }

        return null;
    }

    private static IReadOnlyList<OwnerWorkingHourResponse> FillClosedDays(
        IReadOnlyList<OwnerWorkingHourResponse> openDays)
    {
        return Enumerable.Range(0, 7)
            .Select(day =>
                openDays.SingleOrDefault(candidate => candidate.DayOfWeek == day)
                ?? new OwnerWorkingHourResponse(day, true, null, null))
            .ToList();
    }

    private Guid? GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(userIdValue, out var userId)
            ? userId
            : null;
    }
}
