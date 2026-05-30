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
[Route("api/customer/appointments")]
public class CustomerAppointmentsController : ControllerBase
{
    private readonly AppDbContext dbContext;
    private readonly AppointmentLifecycleService lifecycleService;
    private readonly AppointmentNotificationService notificationService;

    public CustomerAppointmentsController(
        AppDbContext dbContext,
        AppointmentLifecycleService lifecycleService,
        AppointmentNotificationService notificationService)
    {
        this.dbContext = dbContext;
        this.lifecycleService = lifecycleService;
        this.notificationService = notificationService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CustomerAppointmentResponse>>> List(
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

        await lifecycleService.ProcessDueAppointmentsAsync(cancellationToken);

        var appointmentsQuery = dbContext.Appointments
            .AsNoTracking()
            .Where(appointment => appointment.CustomerUserId == userId.Value);

        if (!TryApplyAppointmentFilters(
            ref appointmentsQuery,
            status,
            fromUtc,
            toUtc,
            out var errorMessage))
        {
            return BadRequest(new { message = errorMessage });
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
            .OrderBy(row => row.appointment.StartsAtUtc)
            .Select(row => new CustomerAppointmentResponse(
                row.appointment.Id,
                row.appointment.Status.ToString(),
                row.appointment.StartsAtUtc,
                row.appointment.EndsAtUtc,
                row.business.Name,
                row.service.Name,
                ((row.staffUser.FirstName ?? string.Empty) + " " + (row.staffUser.LastName ?? string.Empty)).Trim(),
                row.appointment.PriceAmount,
                row.appointment.CurrencyCode,
                dbContext.BusinessReviews.Any(review => review.AppointmentId == row.appointment.Id)))
            .ToListAsync(cancellationToken);
    }

    [HttpPost("{appointmentId:guid}/cancel")]
    public async Task<ActionResult<CustomerAppointmentDecisionResponse>> Cancel(
        Guid appointmentId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        await lifecycleService.ProcessDueAppointmentsAsync(cancellationToken);

        var appointment = await dbContext.Appointments
            .SingleOrDefaultAsync(
                candidate => candidate.Id == appointmentId && candidate.CustomerUserId == userId.Value,
                cancellationToken);

        if (appointment is null)
        {
            return NotFound();
        }

        if (!appointment.CancelByCustomer(DateTimeOffset.UtcNow))
        {
            return BadRequest(new { message = "This appointment cannot be cancelled." });
        }

        await notificationService.AddBusinessAppointmentCancelledByCustomerAsync(
            appointment,
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CustomerAppointmentDecisionResponse(
            appointment.Id,
            appointment.Status.ToString());
    }

    [HttpPost("{appointmentId:guid}/review")]
    public async Task<ActionResult<CustomerAppointmentReviewResponse>> Review(
        Guid appointmentId,
        CustomerAppointmentReviewRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var validationError = ValidateReviewRequest(request);
        if (validationError is not null)
        {
            return BadRequest(new { message = validationError });
        }

        await lifecycleService.ProcessDueAppointmentsAsync(cancellationToken);

        var appointment = await dbContext.Appointments
            .SingleOrDefaultAsync(
                candidate => candidate.Id == appointmentId && candidate.CustomerUserId == userId.Value,
                cancellationToken);

        if (appointment is null)
        {
            return NotFound();
        }

        if (appointment.Status != AppointmentStatus.Completed)
        {
            return BadRequest(new { message = "Only completed appointments can be reviewed." });
        }

        if (await dbContext.BusinessReviews
                .AsNoTracking()
                .AnyAsync(review => review.AppointmentId == appointment.Id, cancellationToken))
        {
            return Conflict(new { message = "This appointment already has a review." });
        }

        var customer = await dbContext.Users
            .AsNoTracking()
            .Where(candidate => candidate.Id == userId.Value)
            .Select(candidate => new
            {
                candidate.FirstName,
                candidate.LastName
            })
            .SingleAsync(cancellationToken);
        var customerName = UserNames.FormatFullName(customer.FirstName, customer.LastName);
        if (string.IsNullOrWhiteSpace(customerName))
        {
            customerName = "Customer";
        }

        var comment = request.Comment.Trim();
        var review = new BusinessReview
        {
            BusinessId = appointment.BusinessId,
            AppointmentId = appointment.Id,
            CustomerName = customerName,
            CustomerInitial = CreateCustomerInitial(customerName),
            Rating = request.Rating,
            Comment = comment,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            IsPublic = true
        };

        dbContext.BusinessReviews.Add(review);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Created(
            $"/api/customer/appointments/{appointment.Id}/review",
            new CustomerAppointmentReviewResponse(
                review.Id,
                review.AppointmentId!.Value,
                review.BusinessId,
                review.CustomerName,
                review.CustomerInitial,
                review.Rating,
                review.Comment,
                review.CreatedAtUtc));
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

    private static string? ValidateReviewRequest(CustomerAppointmentReviewRequest request)
    {
        if (request.Rating is < 1 or > 5)
        {
            return "Rating must be between 1 and 5.";
        }

        if (string.IsNullOrWhiteSpace(request.Comment))
        {
            return "Review comment is required.";
        }

        if (request.Comment.Trim().Length > 1200)
        {
            return "Review comment cannot exceed 1200 characters.";
        }

        return null;
    }

    private static string CreateCustomerInitial(string customerName)
    {
        var initials = customerName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(2)
            .Select(part => char.ToUpperInvariant(part[0]))
            .ToArray();

        return initials.Length == 0
            ? "C"
            : new string(initials);
    }
}

public sealed record CustomerAppointmentResponse(
    Guid Id,
    string Status,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    string BusinessName,
    string ServiceName,
    string StaffDisplayName,
    decimal PriceAmount,
    string CurrencyCode,
    bool HasReview);

public sealed record CustomerAppointmentDecisionResponse(
    Guid Id,
    string Status);

public sealed record CustomerAppointmentReviewRequest(
    int Rating,
    string Comment);

public sealed record CustomerAppointmentReviewResponse(
    Guid Id,
    Guid AppointmentId,
    Guid BusinessId,
    string CustomerName,
    string CustomerInitial,
    decimal Rating,
    string Comment,
    DateTimeOffset CreatedAtUtc);
