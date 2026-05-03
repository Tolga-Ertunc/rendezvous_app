using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rendezvous.Domain.Appointments;
using Rendezvous.Domain.Businesses;
using Rendezvous.Infrastructure.Persistence;

namespace Rendezvous.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/booking/businesses/{businessId:guid}/services/{serviceId:guid}/availability")]
public class BookingAvailabilityController : ControllerBase
{
    private static readonly TimeSpan SlotStep = TimeSpan.FromMinutes(15);
    private readonly AppDbContext dbContext;

    public BookingAvailabilityController(AppDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<BookingAvailabilityResponse>> Get(
        Guid businessId,
        Guid serviceId,
        [FromQuery] DateOnly date,
        CancellationToken cancellationToken)
    {
        var business = await dbContext.Businesses
            .AsNoTracking()
            .Where(candidate => candidate.Id == businessId && candidate.Status == BusinessStatus.Approved)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.TimeZoneId
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (business is null)
        {
            return NotFound();
        }

        var service = await dbContext.BusinessServices
            .AsNoTracking()
            .Where(candidate =>
                candidate.Id == serviceId
                && candidate.BusinessId == businessId
                && candidate.IsActive)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.DurationMinutes
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (service is null)
        {
            return NotFound();
        }

        var businessWorkingHour = await dbContext.BusinessWorkingHours
            .AsNoTracking()
            .Where(workingHour =>
                workingHour.BusinessId == businessId
                && workingHour.DayOfWeek == date.DayOfWeek)
            .Select(workingHour => new
            {
                workingHour.OpensAt,
                workingHour.ClosesAt
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (businessWorkingHour is null)
        {
            return new BookingAvailabilityResponse(
                date,
                service.Id,
                service.DurationMinutes,
                []);
        }

        var staffWorkingQueryRows = await dbContext.StaffMembers
            .AsNoTracking()
            .Where(staffMember => staffMember.BusinessId == businessId && staffMember.IsActive)
            .Join(
                dbContext.StaffWorkingHours.AsNoTracking()
                    .Where(workingHour => workingHour.DayOfWeek == date.DayOfWeek),
                staffMember => staffMember.Id,
                workingHour => workingHour.StaffMemberId,
                (staffMember, workingHour) => new
                {
                    StaffMemberId = staffMember.Id,
                    staffMember.DisplayName,
                    workingHour.StartsAt,
                    workingHour.EndsAt
                })
            .OrderBy(row => row.DisplayName)
            .ToListAsync(cancellationToken);
        var staffWorkingRows = staffWorkingQueryRows
            .Select(row => new StaffWorkingRow(
                row.StaffMemberId,
                row.DisplayName,
                row.StartsAt,
                row.EndsAt))
            .ToList();

        if (staffWorkingRows.Count == 0)
        {
            return new BookingAvailabilityResponse(
                date,
                service.Id,
                service.DurationMinutes,
                []);
        }

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(business.TimeZoneId);
        var dayStartUtc = ConvertLocalToUtc(date, TimeOnly.MinValue, timeZone);
        var dayEndUtc = ConvertLocalToUtc(date.AddDays(1), TimeOnly.MinValue, timeZone);
        var staffIds = staffWorkingRows.Select(row => row.StaffMemberId).ToList();
        var approvedAppointments = await dbContext.Appointments
            .AsNoTracking()
            .Where(appointment =>
                staffIds.Contains(appointment.StaffMemberId)
                && appointment.Status == AppointmentStatus.Approved
                && appointment.StartsAtUtc < dayEndUtc
                && appointment.EndsAtUtc > dayStartUtc)
            .Select(appointment => new AppointmentBusyRange(
                appointment.StaffMemberId,
                appointment.StartsAtUtc,
                appointment.EndsAtUtc))
            .ToListAsync(cancellationToken);

        var duration = TimeSpan.FromMinutes(service.DurationMinutes);
        var slots = BuildSlots(
            date,
            timeZone,
            businessWorkingHour.OpensAt,
            businessWorkingHour.ClosesAt,
            staffWorkingRows,
            approvedAppointments,
            duration,
            DateTimeOffset.UtcNow);

        return new BookingAvailabilityResponse(
            date,
            service.Id,
            service.DurationMinutes,
            slots);
    }

    private static IReadOnlyList<AvailabilitySlotResponse> BuildSlots(
        DateOnly date,
        TimeZoneInfo timeZone,
        TimeOnly businessOpensAt,
        TimeOnly businessClosesAt,
        IReadOnlyList<StaffWorkingRow> staffWorkingRows,
        IReadOnlyList<AppointmentBusyRange> approvedAppointments,
        TimeSpan duration,
        DateTimeOffset nowUtc)
    {
        var slots = new Dictionary<SlotKey, List<AvailableStaffResponse>>();

        foreach (var staffWorkingRow in staffWorkingRows)
        {
            var startsAt = Max(businessOpensAt, staffWorkingRow.StartsAt);
            var endsAt = Min(businessClosesAt, staffWorkingRow.EndsAt);

            for (var current = startsAt; current.ToTimeSpan() + duration <= endsAt.ToTimeSpan(); current = current.Add(SlotStep))
            {
                var slotEnd = current.Add(duration);
                var startsAtUtc = ConvertLocalToUtc(date, current, timeZone);
                var endsAtUtc = ConvertLocalToUtc(date, slotEnd, timeZone);

                if (startsAtUtc <= nowUtc)
                {
                    continue;
                }

                if (HasApprovedOverlap(staffWorkingRow.StaffMemberId, startsAtUtc, endsAtUtc, approvedAppointments))
                {
                    continue;
                }

                var key = new SlotKey(startsAtUtc, endsAtUtc, current, slotEnd);
                if (!slots.TryGetValue(key, out var staffMembers))
                {
                    staffMembers = [];
                    slots[key] = staffMembers;
                }

                staffMembers.Add(new AvailableStaffResponse(
                    staffWorkingRow.StaffMemberId,
                    staffWorkingRow.DisplayName));
            }
        }

        return slots
            .OrderBy(slot => slot.Key.StartsAtUtc)
            .Select(slot => new AvailabilitySlotResponse(
                slot.Key.StartsAtUtc,
                slot.Key.EndsAtUtc,
                slot.Key.StartsAtLocal.ToString("HH:mm"),
                slot.Key.EndsAtLocal.ToString("HH:mm"),
                slot.Value
                    .OrderBy(staffMember => staffMember.DisplayName)
                    .ToList()))
            .ToList();
    }

    private static bool HasApprovedOverlap(
        Guid staffMemberId,
        DateTimeOffset startsAtUtc,
        DateTimeOffset endsAtUtc,
        IReadOnlyList<AppointmentBusyRange> approvedAppointments)
    {
        return approvedAppointments.Any(appointment =>
            appointment.StaffMemberId == staffMemberId
            && startsAtUtc < appointment.EndsAtUtc
            && endsAtUtc > appointment.StartsAtUtc);
    }

    private static DateTimeOffset ConvertLocalToUtc(DateOnly date, TimeOnly time, TimeZoneInfo timeZone)
    {
        var localDateTime = DateTime.SpecifyKind(date.ToDateTime(time), DateTimeKind.Unspecified);
        var utcDateTime = TimeZoneInfo.ConvertTimeToUtc(localDateTime, timeZone);

        return new DateTimeOffset(utcDateTime, TimeSpan.Zero);
    }

    private static TimeOnly Max(TimeOnly first, TimeOnly second)
    {
        return first >= second ? first : second;
    }

    private static TimeOnly Min(TimeOnly first, TimeOnly second)
    {
        return first <= second ? first : second;
    }

    private sealed record StaffWorkingRow(
        Guid StaffMemberId,
        string DisplayName,
        TimeOnly StartsAt,
        TimeOnly EndsAt);

    private sealed record AppointmentBusyRange(
        Guid StaffMemberId,
        DateTimeOffset StartsAtUtc,
        DateTimeOffset EndsAtUtc);

    private sealed record SlotKey(
        DateTimeOffset StartsAtUtc,
        DateTimeOffset EndsAtUtc,
        TimeOnly StartsAtLocal,
        TimeOnly EndsAtLocal);
}

public sealed record BookingAvailabilityResponse(
    DateOnly Date,
    Guid ServiceId,
    int DurationMinutes,
    IReadOnlyList<AvailabilitySlotResponse> Slots);

public sealed record AvailabilitySlotResponse(
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    string StartsAtLocal,
    string EndsAtLocal,
    IReadOnlyList<AvailableStaffResponse> StaffMembers);

public sealed record AvailableStaffResponse(
    Guid StaffMemberId,
    string DisplayName);
