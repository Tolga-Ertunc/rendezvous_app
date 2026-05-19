using Microsoft.EntityFrameworkCore;
using Rendezvous.Domain.Appointments;
using Rendezvous.Domain.Availability;
using Rendezvous.Infrastructure.Identity;
using Rendezvous.Infrastructure.Persistence;

namespace Rendezvous.Api.Services;

public class AvailabilityExceptionService
{
    private readonly AppDbContext dbContext;
    private readonly AppointmentNotificationService notificationService;

    public AvailabilityExceptionService(
        AppDbContext dbContext,
        AppointmentNotificationService notificationService)
    {
        this.dbContext = dbContext;
        this.notificationService = notificationService;
    }

    public async Task<IReadOnlyList<AvailabilityException>> GetExceptionsForAvailabilityAsync(
        Guid businessId,
        IReadOnlyList<Guid> staffMemberIds,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        return await dbContext.AvailabilityExceptions
            .AsNoTracking()
            .Where(exception =>
                exception.BusinessId == businessId
                && exception.Date == date
                && (exception.StaffMemberId == null || staffMemberIds.Contains(exception.StaffMemberId.Value)))
            .ToListAsync(cancellationToken);
    }

    public bool IsSlotBlocked(
        Guid staffMemberId,
        TimeOnly startsAt,
        TimeOnly endsAt,
        IReadOnlyList<AvailabilityException> exceptions)
    {
        return exceptions.Any(exception =>
            (exception.StaffMemberId is null || exception.StaffMemberId == staffMemberId)
            && Overlaps(exception, startsAt, endsAt));
    }

    public Task<bool> HasOverlappingExceptionAsync(
        AvailabilityExceptionDraft draft,
        Guid? exceptionIdToIgnore,
        CancellationToken cancellationToken)
    {
        return dbContext.AvailabilityExceptions
            .AsNoTracking()
            .Where(exception =>
                exception.BusinessId == draft.BusinessId
                && exception.Date == draft.Date
                && exception.StaffMemberId == draft.StaffMemberId
                && (!exceptionIdToIgnore.HasValue || exception.Id != exceptionIdToIgnore.Value))
            .AnyAsync(exception =>
                exception.IsFullDay
                || draft.IsFullDay
                || (exception.StartsAt!.Value < draft.EndsAt!.Value
                    && exception.EndsAt!.Value > draft.StartsAt!.Value),
                cancellationToken);
    }

    public async Task<IReadOnlyList<AvailabilityExceptionAppointmentConflict>> GetConflictingAppointmentsAsync(
        AvailabilityExceptionDraft draft,
        string timeZoneId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var range = ToUtcRange(draft, timeZone);

        var conflicts = await dbContext.Appointments
            .AsNoTracking()
            .Where(appointment =>
                appointment.BusinessId == draft.BusinessId
                && (!draft.StaffMemberId.HasValue || appointment.StaffMemberId == draft.StaffMemberId.Value)
                && (appointment.Status == AppointmentStatus.Pending || appointment.Status == AppointmentStatus.Approved)
                && appointment.StartsAtUtc >= nowUtc
                && appointment.StartsAtUtc < range.EndsAtUtc
                && appointment.EndsAtUtc > range.StartsAtUtc)
            .Join(
                dbContext.BusinessServices.AsNoTracking(),
                appointment => appointment.BusinessServiceId,
                service => service.Id,
                (appointment, service) => new { appointment, service })
            .Join(
                dbContext.StaffMembers.AsNoTracking(),
                row => row.appointment.StaffMemberId,
                staffMember => staffMember.Id,
                (row, staffMember) => new { row.appointment, row.service, staffMember })
            .Join(
                dbContext.Users.AsNoTracking(),
                row => row.staffMember.UserId,
                user => user.Id,
                (row, user) => new
                {
                    row.appointment.Id,
                    Status = row.appointment.Status.ToString(),
                    row.appointment.StartsAtUtc,
                    row.appointment.EndsAtUtc,
                    ServiceName = row.service.Name,
                    FirstName = user.FirstName ?? string.Empty,
                    LastName = user.LastName ?? string.Empty
                })
            .OrderBy(conflict => conflict.StartsAtUtc)
            .ToListAsync(cancellationToken);

        return conflicts
            .Select(conflict => new AvailabilityExceptionAppointmentConflict(
                conflict.Id,
                conflict.Status,
                conflict.StartsAtUtc,
                conflict.EndsAtUtc,
                conflict.ServiceName,
                UserNames.FormatFullName(conflict.FirstName, conflict.LastName)))
            .ToList();
    }

    public async Task<int> CancelConflictingAppointmentsAsync(
        AvailabilityExceptionDraft draft,
        string timeZoneId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var range = ToUtcRange(draft, timeZone);
        var appointments = await dbContext.Appointments
            .Where(appointment =>
                appointment.BusinessId == draft.BusinessId
                && (!draft.StaffMemberId.HasValue || appointment.StaffMemberId == draft.StaffMemberId.Value)
                && (appointment.Status == AppointmentStatus.Pending || appointment.Status == AppointmentStatus.Approved)
                && appointment.StartsAtUtc >= nowUtc
                && appointment.StartsAtUtc < range.EndsAtUtc
                && appointment.EndsAtUtc > range.StartsAtUtc)
            .ToListAsync(cancellationToken);

        foreach (var appointment in appointments)
        {
            appointment.Status = AppointmentStatus.Cancelled;
            notificationService.AddCustomerAppointmentCancelled(appointment);
        }

        return appointments.Count;
    }

    private static bool Overlaps(AvailabilityException exception, TimeOnly startsAt, TimeOnly endsAt)
    {
        return exception.IsFullDay
            || (exception.StartsAt!.Value < endsAt && exception.EndsAt!.Value > startsAt);
    }

    private static AvailabilityExceptionUtcRange ToUtcRange(
        AvailabilityExceptionDraft draft,
        TimeZoneInfo timeZone)
    {
        var startsAt = draft.IsFullDay ? TimeOnly.MinValue : draft.StartsAt!.Value;
        var endsAt = draft.IsFullDay ? TimeOnly.MinValue : draft.EndsAt!.Value;
        var endDate = draft.IsFullDay ? draft.Date.AddDays(1) : draft.Date;

        return new AvailabilityExceptionUtcRange(
            ConvertLocalToUtc(draft.Date, startsAt, timeZone),
            ConvertLocalToUtc(endDate, endsAt, timeZone));
    }

    private static DateTimeOffset ConvertLocalToUtc(DateOnly date, TimeOnly time, TimeZoneInfo timeZone)
    {
        var localDateTime = DateTime.SpecifyKind(date.ToDateTime(time), DateTimeKind.Unspecified);
        var utcDateTime = TimeZoneInfo.ConvertTimeToUtc(localDateTime, timeZone);

        return new DateTimeOffset(utcDateTime, TimeSpan.Zero);
    }

    private sealed record AvailabilityExceptionUtcRange(
        DateTimeOffset StartsAtUtc,
        DateTimeOffset EndsAtUtc);
}

public sealed record AvailabilityExceptionDraft(
    Guid BusinessId,
    Guid? StaffMemberId,
    AvailabilityExceptionType Type,
    DateOnly Date,
    bool IsFullDay,
    TimeOnly? StartsAt,
    TimeOnly? EndsAt);

public sealed record AvailabilityExceptionAppointmentConflict(
    Guid Id,
    string Status,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    string ServiceName,
    string StaffDisplayName);
