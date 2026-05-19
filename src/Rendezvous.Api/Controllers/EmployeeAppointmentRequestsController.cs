using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rendezvous.Api.Services;
using Rendezvous.Domain.Appointments;
using Rendezvous.Domain.Businesses;
using Rendezvous.Domain.Notifications;
using Rendezvous.Infrastructure.Persistence;

namespace Rendezvous.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/employee/appointment-requests")]
public class EmployeeAppointmentRequestsController : ControllerBase
{
    private readonly AppDbContext dbContext;
    private readonly AppointmentLifecycleService lifecycleService;
    private readonly NotificationWriter notificationWriter;
    private readonly AppointmentEmailService appointmentEmailService;

    public EmployeeAppointmentRequestsController(
        AppDbContext dbContext,
        AppointmentLifecycleService lifecycleService,
        NotificationWriter notificationWriter,
        AppointmentEmailService appointmentEmailService)
    {
        this.dbContext = dbContext;
        this.lifecycleService = lifecycleService;
        this.notificationWriter = notificationWriter;
        this.appointmentEmailService = appointmentEmailService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EmployeeAppointmentRequestResponse>>> List(
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        await lifecycleService.ProcessDueAppointmentsAsync(cancellationToken);

        return await GetEmployeeAppointmentRequestsQuery(userId.Value)
            .ToListAsync(cancellationToken);
    }

    [HttpPost("{appointmentId:guid}/approve")]
    public async Task<ActionResult<EmployeeAppointmentRequestDecisionResponse>> Approve(
        Guid appointmentId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var appointment = await GetEmployeeAppointmentQuery(userId.Value, appointmentId)
            .SingleOrDefaultAsync(cancellationToken);

        if (appointment is null)
        {
            return NotFound();
        }

        if (appointment.Status != AppointmentStatus.Pending)
        {
            return BadRequest(new { message = "Only pending appointment requests can be approved." });
        }

        var hasApprovedOverlap = await dbContext.Appointments
            .AsNoTracking()
            .AnyAsync(
                candidate =>
                    candidate.Id != appointment.Id
                    && candidate.StaffMemberId == appointment.StaffMemberId
                    && candidate.Status == AppointmentStatus.Approved
                    && appointment.StartsAtUtc < candidate.EndsAtUtc
                    && appointment.EndsAtUtc > candidate.StartsAtUtc,
                cancellationToken);

        if (hasApprovedOverlap)
        {
            return Conflict(new { message = "The selected slot already has an approved appointment." });
        }

        var overlappingPendingRequests = await dbContext.Appointments
            .Where(candidate =>
                candidate.Id != appointment.Id
                && candidate.StaffMemberId == appointment.StaffMemberId
                && candidate.Status == AppointmentStatus.Pending
                && appointment.StartsAtUtc < candidate.EndsAtUtc
                && appointment.EndsAtUtc > candidate.StartsAtUtc)
            .ToListAsync(cancellationToken);

        appointment.Status = AppointmentStatus.Approved;
        foreach (var overlappingRequest in overlappingPendingRequests)
        {
            overlappingRequest.Status = AppointmentStatus.Rejected;
            notificationWriter.Add(
                overlappingRequest.CustomerUserId,
                "Appointment request rejected",
                "Your appointment request was rejected because the slot is no longer available.",
                NotificationType.AppointmentRequestRejected,
                "/appointments");
        }

        notificationWriter.Add(
            appointment.CustomerUserId,
            "Appointment request approved",
            "Your appointment request was approved.",
            NotificationType.AppointmentRequestApproved,
            "/appointments");

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await appointmentEmailService.SendApprovalEmailAsync(appointment.Id, cancellationToken);

        return new EmployeeAppointmentRequestDecisionResponse(
            appointment.Id,
            appointment.Status.ToString(),
            overlappingPendingRequests.Count);
    }

    [HttpPost("{appointmentId:guid}/reject")]
    public async Task<ActionResult<EmployeeAppointmentRequestDecisionResponse>> Reject(
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

        if (appointment.Status != AppointmentStatus.Pending)
        {
            return BadRequest(new { message = "Only pending appointment requests can be rejected." });
        }

        appointment.Status = AppointmentStatus.Rejected;
        notificationWriter.Add(
            appointment.CustomerUserId,
            "Appointment request rejected",
            "Your appointment request was rejected.",
            NotificationType.AppointmentRequestRejected,
            "/appointments");
        await dbContext.SaveChangesAsync(cancellationToken);

        return new EmployeeAppointmentRequestDecisionResponse(
            appointment.Id,
            appointment.Status.ToString(),
            0);
    }

    private IQueryable<EmployeeAppointmentRequestResponse> GetEmployeeAppointmentRequestsQuery(Guid userId)
    {
        return dbContext.Appointments
            .AsNoTracking()
            .Where(appointment => appointment.Status == AppointmentStatus.Pending)
            .Join(
                dbContext.StaffMembers.AsNoTracking().Where(staffMember =>
                    staffMember.UserId == userId
                    && staffMember.IsActive),
                appointment => appointment.StaffMemberId,
                staffMember => staffMember.Id,
                (appointment, staffMember) => new { appointment, staffMember })
            .Join(
                dbContext.BusinessMemberships.AsNoTracking().Where(membership =>
                    membership.UserId == userId
                    && membership.Role == BusinessMembershipRole.Employee
                    && membership.Status == BusinessMembershipStatus.Active),
                row => row.appointment.BusinessId,
                membership => membership.BusinessId,
                (row, membership) => row)
            .Join(
                dbContext.Businesses.AsNoTracking(),
                row => row.appointment.BusinessId,
                business => business.Id,
                (row, business) => new { row.appointment, row.staffMember, business })
            .Join(
                dbContext.BusinessServices.AsNoTracking(),
                row => row.appointment.BusinessServiceId,
                service => service.Id,
                (row, service) => new { row.appointment, row.staffMember, row.business, service })
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
            .Select(row => new EmployeeAppointmentRequestResponse(
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
                row.appointment.CurrencyCode));
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
                (row, membership) => row.appointment);
    }

    private Guid? GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(userIdValue, out var userId)
            ? userId
            : null;
    }
}

public sealed record EmployeeAppointmentRequestResponse(
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

public sealed record EmployeeAppointmentRequestDecisionResponse(
    Guid Id,
    string Status,
    int AutoRejectedCount);
