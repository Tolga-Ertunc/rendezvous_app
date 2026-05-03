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
[Route("api/owner/businesses/{businessId:guid}/working-hours")]
public class OwnerWorkingHoursController : ControllerBase
{
    private readonly AppDbContext dbContext;

    public OwnerWorkingHoursController(AppDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OwnerWorkingHourResponse>>> Get(
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

        var rows = await dbContext.BusinessWorkingHours
            .AsNoTracking()
            .Where(workingHour => workingHour.BusinessId == businessId)
            .OrderBy(workingHour => workingHour.DayOfWeek)
            .Select(workingHour => new OwnerWorkingHourResponse(
                (int)workingHour.DayOfWeek,
                false,
                workingHour.OpensAt.ToString("HH:mm"),
                workingHour.ClosesAt.ToString("HH:mm")))
            .ToListAsync(cancellationToken);

        return Ok(FillClosedDays(rows));
    }

    [HttpPut]
    public async Task<ActionResult<IReadOnlyList<OwnerWorkingHourResponse>>> Update(
        Guid businessId,
        IReadOnlyList<OwnerWorkingHourRequest> request,
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

        var validationError = ValidateWorkingHours(request);
        if (validationError is not null)
        {
            return BadRequest(new { message = validationError });
        }

        var existingRows = await dbContext.BusinessWorkingHours
            .Where(workingHour => workingHour.BusinessId == businessId)
            .ToListAsync(cancellationToken);

        foreach (var day in Enumerable.Range(0, 7))
        {
            var requestedDay = request.SingleOrDefault(candidate => candidate.DayOfWeek == day);
            var existingRow = existingRows.SingleOrDefault(candidate => (int)candidate.DayOfWeek == day);

            if (requestedDay is null || requestedDay.IsClosed)
            {
                if (existingRow is not null)
                {
                    dbContext.BusinessWorkingHours.Remove(existingRow);
                }

                continue;
            }

            var opensAt = TimeOnly.Parse(requestedDay.OpensAt!);
            var closesAt = TimeOnly.Parse(requestedDay.ClosesAt!);

            if (existingRow is null)
            {
                dbContext.BusinessWorkingHours.Add(new BusinessWorkingHour
                {
                    BusinessId = businessId,
                    DayOfWeek = (DayOfWeek)day,
                    OpensAt = opensAt,
                    ClosesAt = closesAt
                });

                continue;
            }

            existingRow.OpensAt = opensAt;
            existingRow.ClosesAt = closesAt;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return await Get(businessId, cancellationToken);
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

            if (!TimeOnly.TryParse(day.OpensAt, out var opensAt)
                || !TimeOnly.TryParse(day.ClosesAt, out var closesAt))
            {
                return "Open days must include valid opensAt and closesAt values.";
            }

            if (opensAt >= closesAt)
            {
                return "opensAt must be earlier than closesAt.";
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

public sealed record OwnerWorkingHourRequest(
    int DayOfWeek,
    bool IsClosed,
    string? OpensAt,
    string? ClosesAt);

public sealed record OwnerWorkingHourResponse(
    int DayOfWeek,
    bool IsClosed,
    string? OpensAt,
    string? ClosesAt);
