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

        var rows = await (
            from appointment in appointmentsQuery
            join business in dbContext.Businesses.AsNoTracking()
                on appointment.BusinessId equals business.Id
            join service in dbContext.BusinessServices.AsNoTracking()
                on appointment.BusinessServiceId equals service.Id
            join staffMember in dbContext.StaffMembers.AsNoTracking()
                on appointment.StaffMemberId equals staffMember.Id
            join staffUser in dbContext.Users.AsNoTracking()
                on staffMember.UserId equals staffUser.Id
            join customerUser in dbContext.Users.AsNoTracking()
                on appointment.CustomerUserId equals customerUser.Id
            join preview in dbContext.AppointmentStylePreviews.AsNoTracking()
                on appointment.Id equals preview.AppointmentId into previewRows
            from preview in previewRows.DefaultIfEmpty()
            orderby appointment.StartsAtUtc
            select new EmployeeAppointmentRow(
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
            .Select(row => new EmployeeAppointmentResponse(
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

    private sealed record EmployeeAppointmentRow(
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
    string CurrencyCode,
    AppointmentStylePreviewResponse? StylePreview);

public sealed record EmployeeAppointmentDecisionResponse(
    Guid Id,
    string Status);
