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
[Route("api/owner/businesses/{businessId:guid}/appointments")]
public class OwnerAppointmentsController : ControllerBase
{
    private readonly AppDbContext dbContext;
    private readonly AppointmentNotificationService notificationService;

    public OwnerAppointmentsController(
        AppDbContext dbContext,
        AppointmentNotificationService notificationService)
    {
        this.dbContext = dbContext;
        this.notificationService = notificationService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OwnerAppointmentResponse>>> List(
        Guid businessId,
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

        if (!await HasActiveOwnerMembershipAsync(businessId, userId.Value, cancellationToken))
        {
            return NotFound();
        }

        var nowUtc = DateTimeOffset.UtcNow;
        var hasFilters = !string.IsNullOrWhiteSpace(status) || fromUtc.HasValue || toUtc.HasValue;
        var appointmentsQuery = dbContext.Appointments
            .AsNoTracking()
            .Where(appointment => appointment.BusinessId == businessId);

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
                (row, staffUser) => new { row.appointment, row.service, staffUser })
            .Join(
                dbContext.Users.AsNoTracking(),
                row => row.appointment.CustomerUserId,
                user => user.Id,
                (row, user) => new { row.appointment, row.service, row.staffUser, user })
            .OrderBy(row => row.appointment.StartsAtUtc)
            .Select(row => new OwnerAppointmentResponse(
                row.appointment.Id,
                row.appointment.Status.ToString(),
                row.appointment.StartsAtUtc,
                row.appointment.EndsAtUtc,
                row.service.Name,
                ((row.staffUser.FirstName ?? string.Empty) + " " + (row.staffUser.LastName ?? string.Empty)).Trim(),
                ((row.user.FirstName ?? string.Empty) + " " + (row.user.LastName ?? string.Empty)).Trim(),
                row.appointment.PriceAmount,
                row.appointment.CurrencyCode))
            .ToListAsync(cancellationToken);
    }

    [HttpPost("{appointmentId:guid}/cancel")]
    public async Task<ActionResult<OwnerAppointmentDecisionResponse>> Cancel(
        Guid businessId,
        Guid appointmentId,
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

        var appointment = await dbContext.Appointments
            .SingleOrDefaultAsync(
                candidate => candidate.Id == appointmentId && candidate.BusinessId == businessId,
                cancellationToken);

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

        return new OwnerAppointmentDecisionResponse(
            appointment.Id,
            appointment.Status.ToString());
    }

    [HttpPost("{appointmentId:guid}/complete")]
    public async Task<ActionResult<OwnerAppointmentDecisionResponse>> Complete(
        Guid businessId,
        Guid appointmentId,
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

        var appointment = await dbContext.Appointments
            .SingleOrDefaultAsync(
                candidate => candidate.Id == appointmentId && candidate.BusinessId == businessId,
                cancellationToken);

        if (appointment is null)
        {
            return NotFound();
        }

        if (!appointment.CompleteApprovedAppointment(DateTimeOffset.UtcNow, automatic: false))
        {
            return BadRequest(new { message = "This appointment cannot be completed." });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new OwnerAppointmentDecisionResponse(
            appointment.Id,
            appointment.Status.ToString());
    }

    [HttpPost("{appointmentId:guid}/no-show")]
    public async Task<ActionResult<OwnerAppointmentDecisionResponse>> MarkNoShow(
        Guid businessId,
        Guid appointmentId,
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

        var appointment = await dbContext.Appointments
            .SingleOrDefaultAsync(
                candidate => candidate.Id == appointmentId && candidate.BusinessId == businessId,
                cancellationToken);

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

        return new OwnerAppointmentDecisionResponse(
            appointment.Id,
            appointment.Status.ToString());
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

public sealed record OwnerAppointmentResponse(
    Guid Id,
    string Status,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    string ServiceName,
    string StaffDisplayName,
    string CustomerFullName,
    decimal PriceAmount,
    string CurrencyCode);

public sealed record OwnerAppointmentDecisionResponse(
    Guid Id,
    string Status);
