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
[Route("api/owner/businesses/{businessId:guid}/appointment-requests")]
public class OwnerAppointmentRequestsController : ControllerBase
{
    private readonly AppDbContext dbContext;
    private readonly AppointmentExpirationService expirationService;
    private readonly NotificationWriter notificationWriter;
    private readonly AppointmentEmailService appointmentEmailService;

    public OwnerAppointmentRequestsController(
        AppDbContext dbContext,
        AppointmentExpirationService expirationService,
        NotificationWriter notificationWriter,
        AppointmentEmailService appointmentEmailService)
    {
        this.dbContext = dbContext;
        this.expirationService = expirationService;
        this.notificationWriter = notificationWriter;
        this.appointmentEmailService = appointmentEmailService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OwnerAppointmentRequestResponse>>> List(
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

        await expirationService.ExpirePendingAppointmentsAsync(cancellationToken);

        return await GetAppointmentRequestsQuery(businessId)
            .ToListAsync(cancellationToken);
    }

    [HttpPost("{appointmentId:guid}/approve")]
    public async Task<ActionResult<OwnerAppointmentRequestDecisionResponse>> Approve(
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

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var appointment = await dbContext.Appointments
            .SingleOrDefaultAsync(
                candidate => candidate.Id == appointmentId && candidate.BusinessId == businessId,
                cancellationToken);

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

        return new OwnerAppointmentRequestDecisionResponse(
            appointment.Id,
            appointment.Status.ToString(),
            overlappingPendingRequests.Count);
    }

    [HttpPost("{appointmentId:guid}/reject")]
    public async Task<ActionResult<OwnerAppointmentRequestDecisionResponse>> Reject(
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

        return new OwnerAppointmentRequestDecisionResponse(
            appointment.Id,
            appointment.Status.ToString(),
            0);
    }

    private IQueryable<OwnerAppointmentRequestResponse> GetAppointmentRequestsQuery(Guid businessId)
    {
        return dbContext.Appointments
            .AsNoTracking()
            .Where(appointment =>
                appointment.BusinessId == businessId
                && appointment.Status == AppointmentStatus.Pending)
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
                row => row.appointment.CustomerUserId,
                user => user.Id,
                (row, user) => new { row.appointment, row.service, row.staffMember, user })
            .OrderBy(row => row.appointment.StartsAtUtc)
            .Select(row => new OwnerAppointmentRequestResponse(
                row.appointment.Id,
                row.appointment.Status.ToString(),
                row.appointment.StartsAtUtc,
                row.appointment.EndsAtUtc,
                row.service.Name,
                row.staffMember.DisplayName,
                ((row.user.FirstName ?? string.Empty) + " " + (row.user.LastName ?? string.Empty)).Trim(),
                row.appointment.PriceAmount,
                row.appointment.CurrencyCode));
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

    private Guid? GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(userIdValue, out var userId)
            ? userId
            : null;
    }
}

public sealed record OwnerAppointmentRequestResponse(
    Guid Id,
    string Status,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    string ServiceName,
    string StaffDisplayName,
    string CustomerFullName,
    decimal PriceAmount,
    string CurrencyCode);

public sealed record OwnerAppointmentRequestDecisionResponse(
    Guid Id,
    string Status,
    int AutoRejectedCount);
