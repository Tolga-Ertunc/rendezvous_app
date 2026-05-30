using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rendezvous.Api.Services;
using Rendezvous.Domain.Appointments;
using Rendezvous.Domain.Businesses;
using Rendezvous.Infrastructure.Identity;
using Rendezvous.Infrastructure.Persistence;

namespace Rendezvous.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/employee/appointments")]
public class EmployeeAppointmentsController : ControllerBase
{
    private readonly AppDbContext dbContext;
    private readonly AppointmentNotificationService notificationService;

    public EmployeeAppointmentsController(
        AppDbContext dbContext,
        AppointmentNotificationService notificationService)
    {
        this.dbContext = dbContext;
        this.notificationService = notificationService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EmployeeAppointmentResponse>>> List(
        [FromQuery] string? status,
        [FromQuery] DateTimeOffset? fromUtc,
        [FromQuery] DateTimeOffset? toUtc,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var nowUtc = DateTimeOffset.UtcNow;
        var hasFilters = !string.IsNullOrWhiteSpace(status) || fromUtc.HasValue || toUtc.HasValue;
        var appointmentsQuery = dbContext.Appointments
            .AsNoTracking()
            .Join(
                dbContext.StaffMembers.AsNoTracking().Where(staffMember =>
                    staffMember.UserId == userId.Value
                    && staffMember.IsActive),
                appointment => appointment.StaffMemberId,
                staffMember => staffMember.Id,
                (appointment, staffMember) => new { appointment, staffMember })
            .Join(
                dbContext.BusinessMemberships.AsNoTracking().Where(membership =>
                    membership.UserId == userId.Value
                    && membership.Role == BusinessMembershipRole.Employee
                    && membership.Status == BusinessMembershipStatus.Active),
                row => row.appointment.BusinessId,
                membership => membership.BusinessId,
                (row, _) => row.appointment);

        if (hasFilters)
        {
            if (!TryApplyAppointmentFilters(
                ref appointmentsQuery,
                status,
                fromUtc,
                toUtc,
                out var errorMessage))
            {
                return BadRequest(new { message = errorMessage });
            }
        }
        else
        {
            appointmentsQuery = appointmentsQuery.Where(appointment =>
                appointment.Status == AppointmentStatus.Approved
                && appointment.StartsAtUtc >= nowUtc);
        }

        return await appointmentsQuery
            .Join(
                dbContext.Businesses.AsNoTracking(),
                appointment => appointment.BusinessId,
                business => business.Id,
                (appointment, business) => new { appointment, business })
            .Join(
                dbContext.BusinessServices.AsNoTracking(),
                row => row.appointment.BusinessServiceId,
                service => service.Id,
                (row, service) => new { row.appointment, row.business, service })
            .Join(
                dbContext.StaffMembers.AsNoTracking(),
                row => row.appointment.StaffMemberId,
                staffMember => staffMember.Id,
                (row, staffMember) => new { row.appointment, row.business, row.service, staffMember })
            .Join(
                dbContext.Users.AsNoTracking(),
                row => row.staffMember.UserId,
                user => user.Id,
                (row, staffUser) => new { row.appointment, row.business, row.service, staffUser })
            .Join(
                dbContext.Users.AsNoTracking(),
                row => row.appointment.CustomerUserId,
                user => user.Id,
                (row, user) => new { row.appointment, row.business, row.service, row.staffUser, user })
            .OrderBy(row => row.appointment.StartsAtUtc)
            .Select(row => new EmployeeAppointmentResponse(
                row.appointment.Id,
                row.appointment.Status.ToString(),
                row.appointment.StartsAtUtc,
                row.appointment.EndsAtUtc,
                row.business.Id,
                row.business.Name,
                row.service.Name,
                ((row.staffUser.FirstName ?? string.Empty) + " " + (row.staffUser.LastName ?? string.Empty)).Trim(),
                ((row.user.FirstName ?? string.Empty) + " " + (row.user.LastName ?? string.Empty)).Trim(),
                row.appointment.PriceAmount,
                row.appointment.CurrencyCode))
            .ToListAsync(cancellationToken);
    }

    [HttpPost("{appointmentId:guid}/cancel")]
    public async Task<ActionResult<EmployeeAppointmentDecisionResponse>> Cancel(
        Guid appointmentId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var appointment = await GetEmployeeAppointmentQuery(userId.Value, appointmentId)
            .SingleOrDefaultAsync(cancellationToken);

        if (appointment is null)
        {
            return NotFound();
        }

        if (!appointment.CancelApprovedAppointment(DateTimeOffset.UtcNow))
        {
            return BadRequest(new { message = "This appointment cannot be cancelled." });
        }

        notificationService.AddCustomerAppointmentCancelled(appointment);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new EmployeeAppointmentDecisionResponse(
            appointment.Id,
            appointment.Status.ToString());
    }

    [HttpPost("{appointmentId:guid}/complete")]
    public async Task<ActionResult<EmployeeAppointmentDecisionResponse>> Complete(
        Guid appointmentId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var appointment = await GetEmployeeAppointmentQuery(userId.Value, appointmentId)
            .SingleOrDefaultAsync(cancellationToken);

        if (appointment is null)
        {
            return NotFound();
        }

        if (!appointment.CompleteApprovedAppointment(DateTimeOffset.UtcNow, automatic: false))
        {
            return BadRequest(new { message = "This appointment cannot be completed." });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new EmployeeAppointmentDecisionResponse(
            appointment.Id,
            appointment.Status.ToString());
    }

    [HttpPost("{appointmentId:guid}/no-show")]
    public async Task<ActionResult<EmployeeAppointmentDecisionResponse>> MarkNoShow(
        Guid appointmentId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var appointment = await GetEmployeeAppointmentQuery(userId.Value, appointmentId)
            .SingleOrDefaultAsync(cancellationToken);

        if (appointment is null)
        {
            return NotFound();
        }

        if (appointment.Status == AppointmentStatus.Completed
            && await AppointmentHasReviewAsync(appointment.Id, cancellationToken))
        {
            return BadRequest(new { message = "This appointment already has a review." });
        }

        if (!appointment.MarkNoShow(DateTimeOffset.UtcNow))
        {
            return BadRequest(new { message = "This appointment cannot be marked no-show." });
        }

        notificationService.AddCustomerAppointmentNoShow(appointment);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new EmployeeAppointmentDecisionResponse(
            appointment.Id,
            appointment.Status.ToString());
    }

    private IQueryable<Appointment> GetEmployeeAppointmentQuery(Guid userId, Guid appointmentId)
    {
        return dbContext.Appointments
            .Where(appointment => appointment.Id == appointmentId)
            .Join(
                dbContext.StaffMembers.Where(staffMember =>
                    staffMember.UserId == userId
                    && staffMember.IsActive),
                appointment => appointment.StaffMemberId,
                staffMember => staffMember.Id,
                (appointment, staffMember) => new { appointment, staffMember })
            .Join(
                dbContext.BusinessMemberships.Where(membership =>
                    membership.UserId == userId
                    && membership.Role == BusinessMembershipRole.Employee
                    && membership.Status == BusinessMembershipStatus.Active),
                row => row.appointment.BusinessId,
                membership => membership.BusinessId,
                (row, _) => row.appointment);
    }

    private Task<bool> AppointmentHasReviewAsync(
        Guid appointmentId,
        CancellationToken cancellationToken)
    {
        return dbContext.BusinessReviews
            .AsNoTracking()
            .AnyAsync(review => review.AppointmentId == appointmentId, cancellationToken);
    }

    private Guid? GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(userIdValue, out var userId)
            ? userId
            : null;
    }

    private static bool TryApplyAppointmentFilters(
        ref IQueryable<Appointment> query,
        string? status,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        out string errorMessage)
    {
        errorMessage = string.Empty;

        if (fromUtc.HasValue && toUtc.HasValue && fromUtc > toUtc)
        {
            errorMessage = "fromUtc must be before toUtc.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<AppointmentStatus>(status, ignoreCase: true, out var appointmentStatus))
            {
                errorMessage = "Invalid appointment status.";
                return false;
            }

            query = query.Where(appointment => appointment.Status == appointmentStatus);
        }

        if (fromUtc.HasValue)
        {
            query = query.Where(appointment => appointment.StartsAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(appointment => appointment.StartsAtUtc <= toUtc.Value);
        }

        return true;
    }

}

public sealed record EmployeeAppointmentResponse(
    Guid Id,
    string Status,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    Guid BusinessId,
    string BusinessName,
    string ServiceName,
    string StaffDisplayName,
    string CustomerFullName,
    decimal PriceAmount,
    string CurrencyCode);

public sealed record EmployeeAppointmentDecisionResponse(
    Guid Id,
    string Status);
