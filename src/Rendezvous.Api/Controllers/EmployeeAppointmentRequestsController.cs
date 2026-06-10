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

        return Ok(await GetEmployeeAppointmentRequestsAsync(userId.Value, cancellationToken));
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

    private async Task<IReadOnlyList<EmployeeAppointmentRequestResponse>> GetEmployeeAppointmentRequestsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var rows = await (
            from appointment in dbContext.Appointments.AsNoTracking()
            where appointment.Status == AppointmentStatus.Pending
            join staffMember in dbContext.StaffMembers.AsNoTracking().Where(staffMember =>
                    staffMember.UserId == userId
                    && staffMember.IsActive)
                on appointment.StaffMemberId equals staffMember.Id
            join membership in dbContext.BusinessMemberships.AsNoTracking().Where(membership =>
                    membership.UserId == userId
                    && membership.Role == BusinessMembershipRole.Employee
                    && membership.Status == BusinessMembershipStatus.Active)
                on appointment.BusinessId equals membership.BusinessId
            join business in dbContext.Businesses.AsNoTracking()
                on appointment.BusinessId equals business.Id
            join service in dbContext.BusinessServices.AsNoTracking()
                on appointment.BusinessServiceId equals service.Id
            join staffUser in dbContext.Users.AsNoTracking()
                on staffMember.UserId equals staffUser.Id
            join customerUser in dbContext.Users.AsNoTracking()
                on appointment.CustomerUserId equals customerUser.Id
            join preview in dbContext.AppointmentStylePreviews.AsNoTracking()
                on appointment.Id equals preview.AppointmentId into previewRows
            from preview in previewRows.DefaultIfEmpty()
            orderby appointment.StartsAtUtc
            select new EmployeeAppointmentRequestRow(
                appointment.Id,
                appointment.Status.ToString(),
                appointment.StartsAtUtc,
                appointment.EndsAtUtc,
                business.Id,
                business.Name,
                service.Name,
                ((staffUser.FirstName ?? string.Empty) + " " + (staffUser.LastName ?? string.Empty)).Trim(),
                ((customerUser.FirstName ?? string.Empty) + " " + (customerUser.LastName ?? string.Empty)).Trim(),
                appointment.PriceAmount,
                appointment.CurrencyCode,
                preview == null ? null : preview.Id,
                preview != null && preview.IsPlaceholder))
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new EmployeeAppointmentRequestResponse(
                row.Id,
                row.Status,
                row.StartsAtUtc,
                row.EndsAtUtc,
                row.BusinessId,
                row.BusinessName,
                row.ServiceName,
                row.StaffDisplayName,
                row.CustomerFullName,
                row.PriceAmount,
                row.CurrencyCode,
                BuildStylePreviewResponse(row.StylePreviewId, row.StylePreviewIsPlaceholder)))
            .ToList();
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

    private static AppointmentStylePreviewResponse? BuildStylePreviewResponse(
        Guid? previewId,
        bool isPlaceholder)
    {
        return previewId.HasValue
            ? new AppointmentStylePreviewResponse(
                previewId.Value,
                BuildImageUrl(previewId.Value, "original"),
                BuildImageUrl(previewId.Value, "generated"),
                isPlaceholder)
            : null;
    }

    private static string BuildImageUrl(Guid previewId, string imageKind)
    {
        return $"/backend-api/appointment-style-previews/{previewId}/{imageKind}";
    }

    private sealed record EmployeeAppointmentRequestRow(
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
        string CurrencyCode,
        Guid? StylePreviewId,
        bool StylePreviewIsPlaceholder);
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
    string CurrencyCode,
    AppointmentStylePreviewResponse? StylePreview);

public sealed record EmployeeAppointmentRequestDecisionResponse(
    Guid Id,
    string Status,
    int AutoRejectedCount);
