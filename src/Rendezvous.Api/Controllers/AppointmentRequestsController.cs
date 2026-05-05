using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rendezvous.Api.Services;
using Rendezvous.Domain.Appointments;
using Rendezvous.Domain.Availability;
using Rendezvous.Domain.Businesses;
using Rendezvous.Domain.Notifications;
using Rendezvous.Infrastructure.Identity;
using Rendezvous.Infrastructure.Persistence;

namespace Rendezvous.Api.Controllers;

[ApiController]
[Authorize(Roles = ApplicationRoles.User)]
[Route("api/booking/appointment-requests")]
public class AppointmentRequestsController : ControllerBase
{
    private static readonly TimeSpan SlotStep = TimeSpan.FromMinutes(15);
    private readonly AppDbContext dbContext;
    private readonly AvailabilityExceptionService availabilityExceptionService;
    private readonly NotificationWriter notificationWriter;

    public AppointmentRequestsController(
        AppDbContext dbContext,
        AvailabilityExceptionService availabilityExceptionService,
        NotificationWriter notificationWriter)
    {
        this.dbContext = dbContext;
        this.availabilityExceptionService = availabilityExceptionService;
        this.notificationWriter = notificationWriter;
    }

    [HttpPost]
    public async Task<ActionResult<AppointmentRequestResponse>> Create(
        CreateAppointmentRequest request,
        CancellationToken cancellationToken)
    {
        var customerUserId = GetCurrentUserId();
        if (customerUserId is null)
        {
            return Unauthorized();
        }

        var business = await dbContext.Businesses
            .AsNoTracking()
            .Where(candidate => candidate.Id == request.BusinessId && candidate.Status == BusinessStatus.Approved)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.Name,
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
                candidate.Id == request.ServiceId
                && candidate.BusinessId == request.BusinessId
                && candidate.IsActive)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.DurationMinutes,
                candidate.BasePriceAmount,
                candidate.CurrencyCode
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (service is null)
        {
            return NotFound();
        }

        var staffMember = await dbContext.StaffMembers
            .AsNoTracking()
            .Where(candidate =>
                candidate.Id == request.StaffMemberId
                && candidate.BusinessId == request.BusinessId
                && candidate.IsActive)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.UserId
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (staffMember is null)
        {
            return NotFound();
        }

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(business.TimeZoneId);
        var localStart = TimeZoneInfo.ConvertTime(request.StartsAtUtc, timeZone);
        var localDate = DateOnly.FromDateTime(localStart.DateTime);
        var localStartTime = TimeOnly.FromDateTime(localStart.DateTime);
        var duration = TimeSpan.FromMinutes(service.DurationMinutes);
        var localEndTime = localStartTime.Add(duration);
        var startsAtUtc = request.StartsAtUtc.ToUniversalTime();
        var endsAtUtc = ConvertLocalToUtc(localDate, localEndTime, timeZone);

        if (startsAtUtc <= DateTimeOffset.UtcNow || localStartTime.Ticks % SlotStep.Ticks != 0)
        {
            return BadRequest(new { message = "Selected slot is not available." });
        }

        var businessWorkingHour = await dbContext.BusinessWorkingHours
            .AsNoTracking()
            .SingleOrDefaultAsync(
                workingHour =>
                    workingHour.BusinessId == request.BusinessId
                    && workingHour.DayOfWeek == localDate.DayOfWeek,
                cancellationToken);

        var staffWorkingHour = await dbContext.StaffWorkingHours
            .AsNoTracking()
            .SingleOrDefaultAsync(
                workingHour =>
                    workingHour.StaffMemberId == request.StaffMemberId
                    && workingHour.DayOfWeek == localDate.DayOfWeek,
                cancellationToken);

        if (businessWorkingHour is null
            || staffWorkingHour is null
            || localStartTime < Max(businessWorkingHour.OpensAt, staffWorkingHour.StartsAt)
            || localEndTime > Min(businessWorkingHour.ClosesAt, staffWorkingHour.EndsAt))
        {
            return BadRequest(new { message = "Selected slot is not available." });
        }

        var availabilityExceptions = await availabilityExceptionService.GetExceptionsForAvailabilityAsync(
            request.BusinessId,
            [request.StaffMemberId],
            localDate,
            cancellationToken);

        if (availabilityExceptionService.IsSlotBlocked(
            request.StaffMemberId,
            localStartTime,
            localEndTime,
            availabilityExceptions))
        {
            return BadRequest(new { message = "Selected slot is not available." });
        }

        var hasApprovedOverlap = await dbContext.Appointments
            .AsNoTracking()
            .AnyAsync(
                appointment =>
                    appointment.StaffMemberId == request.StaffMemberId
                    && appointment.Status == AppointmentStatus.Approved
                    && startsAtUtc < appointment.EndsAtUtc
                    && endsAtUtc > appointment.StartsAtUtc,
                cancellationToken);

        if (hasApprovedOverlap)
        {
            return BadRequest(new { message = "Selected slot is not available." });
        }

        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            BusinessId = request.BusinessId,
            BusinessServiceId = request.ServiceId,
            StaffMemberId = request.StaffMemberId,
            CustomerUserId = customerUserId.Value,
            StartsAtUtc = startsAtUtc,
            EndsAtUtc = endsAtUtc,
            Status = AppointmentStatus.Pending,
            PriceAmount = service.BasePriceAmount,
            CurrencyCode = service.CurrencyCode
        };

        dbContext.Appointments.Add(appointment);

        var ownerUserIds = await dbContext.BusinessMemberships
            .AsNoTracking()
            .Where(membership =>
                membership.BusinessId == request.BusinessId
                && membership.Role == BusinessMembershipRole.Owner
                && membership.Status == BusinessMembershipStatus.Active)
            .Select(membership => membership.UserId)
            .ToListAsync(cancellationToken);

        var recipientUserIds = ownerUserIds
            .Append(staffMember.UserId)
            .Distinct()
            .ToList();

        foreach (var recipientUserId in recipientUserIds)
        {
            notificationWriter.Add(
                recipientUserId,
                "New appointment request",
                $"A new request was created for {business.Name}.",
                NotificationType.AppointmentRequestCreated,
                ownerUserIds.Contains(recipientUserId)
                    ? $"/owner/businesses/{business.Id}/appointments"
                    : "/employee/requests");
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Created(
            $"/api/booking/appointment-requests/{appointment.Id}",
            new AppointmentRequestResponse(
                appointment.Id,
                appointment.Status.ToString(),
                appointment.StartsAtUtc,
                appointment.EndsAtUtc,
                appointment.PriceAmount,
                appointment.CurrencyCode));
    }

    private Guid? GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(userIdValue, out var userId)
            ? userId
            : null;
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
}

public sealed record CreateAppointmentRequest(
    Guid BusinessId,
    Guid ServiceId,
    Guid StaffMemberId,
    DateTimeOffset StartsAtUtc);

public sealed record AppointmentRequestResponse(
    Guid Id,
    string Status,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    decimal PriceAmount,
    string CurrencyCode);
