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
    private const int MaxCustomerAppointmentPageSize = 10;

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
    public async Task<ActionResult<CustomerAppointmentsPageResponse>> List(
        [FromQuery] string? view,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string? sort,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        if (!TryParseAppointmentView(view, out var appointmentView, out var viewErrorMessage))
        {
            return BadRequest(new { message = viewErrorMessage });
        }

        if (!TryParseAppointmentSort(sort, out var appointmentSort, out var sortErrorMessage))
        {
            return BadRequest(new { message = sortErrorMessage });
        }

        var nowUtc = DateTimeOffset.UtcNow;
        var normalizedPage = Math.Max(page ?? 1, 1);
        var normalizedPageSize = Math.Clamp(pageSize ?? MaxCustomerAppointmentPageSize, 1, MaxCustomerAppointmentPageSize);

        await lifecycleService.ProcessDueAppointmentsAsync(cancellationToken);

        var customerAppointmentsQuery = dbContext.Appointments
            .AsNoTracking()
            .Where(appointment => appointment.CustomerUserId == userId.Value);

        var summary = new CustomerAppointmentSummaryResponse(
            await customerAppointmentsQuery.CountAsync(cancellationToken),
            await customerAppointmentsQuery.CountAsync(
                appointment => appointment.Status == AppointmentStatus.Pending,
                cancellationToken),
            await customerAppointmentsQuery.CountAsync(
                appointment => appointment.Status == AppointmentStatus.Completed,
                cancellationToken));

        var filteredAppointmentsQuery = ApplyAppointmentView(
            customerAppointmentsQuery,
            appointmentView);
        var totalItems = await filteredAppointmentsQuery.CountAsync(cancellationToken);
        var totalPages = CalculateTotalPages(totalItems, normalizedPageSize);

        if (totalPages > 0 && normalizedPage > totalPages)
        {
            normalizedPage = totalPages;
        }

        var rows = await ApplyAppointmentSort(filteredAppointmentsQuery, appointmentSort)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
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
            .Select(row => new CustomerAppointmentListRow(
                row.appointment.Id,
                row.appointment.Status,
                row.appointment.StartsAtUtc,
                row.appointment.EndsAtUtc,
                row.business.Name,
                row.service.Name,
                row.staffUser.FirstName,
                row.staffUser.LastName,
                row.appointment.PriceAmount,
                row.appointment.CurrencyCode,
                dbContext.BusinessReviews
                    .Where(review => review.AppointmentId == row.appointment.Id)
                    .Select(review => (decimal?)review.Rating)
                    .FirstOrDefault(),
                dbContext.BusinessPhotos
                    .Where(photo => photo.BusinessId == row.business.Id && photo.ImageUrl != string.Empty)
                    .OrderBy(photo => photo.SortOrder)
                    .ThenBy(photo => photo.Id)
                    .Select(photo => photo.ImageUrl)
                    .FirstOrDefault(),
                dbContext.BusinessPhotos
                    .Where(photo => photo.BusinessId == row.business.Id && photo.ImageUrl != string.Empty)
                    .OrderBy(photo => photo.SortOrder)
                    .ThenBy(photo => photo.Id)
                    .Select(photo => photo.AltText)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(row =>
            {
                var hasReview = row.ReviewRating.HasValue;

                return new CustomerAppointmentResponse(
                    row.Id,
                    row.Status.ToString(),
                    row.StartsAtUtc,
                    row.EndsAtUtc,
                    row.BusinessName,
                    row.ServiceName,
                    UserNames.FormatFullName(row.StaffFirstName, row.StaffLastName),
                    row.PriceAmount,
                    row.CurrencyCode,
                    hasReview,
                    string.IsNullOrWhiteSpace(row.BusinessPhotoImageUrl)
                        ? null
                        : new CustomerAppointmentBusinessPhotoResponse(
                            row.BusinessPhotoImageUrl,
                            row.BusinessPhotoAltText ?? string.Empty),
                    row.ReviewRating,
                    CanBeCancelledByCustomer(row.Status, row.StartsAtUtc, nowUtc),
                    row.Status == AppointmentStatus.Completed && !hasReview);
            })
            .ToList();

        return new CustomerAppointmentsPageResponse(
            items,
            summary,
            new CustomerAppointmentsPageMetadataResponse(
                normalizedPage,
                normalizedPageSize,
                totalItems,
                totalPages));
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

    private static bool TryParseAppointmentView(
        string? value,
        out CustomerAppointmentView view,
        out string errorMessage)
    {
        errorMessage = string.Empty;
        var normalizedValue = string.IsNullOrWhiteSpace(value)
            ? "all"
            : value.Trim().ToLowerInvariant();

        view = normalizedValue switch
        {
            "all" => CustomerAppointmentView.All,
            "upcoming" => CustomerAppointmentView.Upcoming,
            "completed" => CustomerAppointmentView.Completed,
            _ => CustomerAppointmentView.All
        };

        if (normalizedValue is "all" or "upcoming" or "completed")
        {
            return true;
        }

        errorMessage = "Invalid appointment view.";
        return false;
    }

    private static bool TryParseAppointmentSort(
        string? value,
        out CustomerAppointmentSort sort,
        out string errorMessage)
    {
        errorMessage = string.Empty;
        var normalizedValue = string.IsNullOrWhiteSpace(value)
            ? "date_desc"
            : value.Trim().ToLowerInvariant();

        sort = normalizedValue switch
        {
            "date_asc" => CustomerAppointmentSort.DateAscending,
            "date_desc" => CustomerAppointmentSort.DateDescending,
            _ => CustomerAppointmentSort.DateDescending
        };

        if (normalizedValue is "date_asc" or "date_desc")
        {
            return true;
        }

        errorMessage = "Invalid appointment sort.";
        return false;
    }

    private static IQueryable<Appointment> ApplyAppointmentView(
        IQueryable<Appointment> query,
        CustomerAppointmentView view)
    {
        return view switch
        {
            CustomerAppointmentView.Upcoming => query.Where(appointment =>
                appointment.Status == AppointmentStatus.Pending
                || appointment.Status == AppointmentStatus.Approved),
            CustomerAppointmentView.Completed => query.Where(appointment =>
                appointment.Status == AppointmentStatus.Completed),
            _ => query
        };
    }

    private static IOrderedQueryable<Appointment> ApplyAppointmentSort(
        IQueryable<Appointment> query,
        CustomerAppointmentSort sort)
    {
        return sort == CustomerAppointmentSort.DateAscending
            ? query
                .OrderBy(appointment => appointment.StartsAtUtc)
                .ThenBy(appointment => appointment.Id)
            : query
                .OrderByDescending(appointment => appointment.StartsAtUtc)
                .ThenByDescending(appointment => appointment.Id);
    }

    private static bool CanBeCancelledByCustomer(
        AppointmentStatus status,
        DateTimeOffset startsAtUtc,
        DateTimeOffset nowUtc)
    {
        if (status == AppointmentStatus.Pending)
        {
            return true;
        }

        return status == AppointmentStatus.Approved
            && startsAtUtc - nowUtc >= TimeSpan.FromHours(1);
    }

    private static int CalculateTotalPages(int totalItems, int pageSize)
    {
        return totalItems == 0
            ? 0
            : (int)Math.Ceiling(totalItems / (double)pageSize);
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

public sealed record CustomerAppointmentsPageResponse(
    IReadOnlyList<CustomerAppointmentResponse> Items,
    CustomerAppointmentSummaryResponse Summary,
    CustomerAppointmentsPageMetadataResponse Page);

public sealed record CustomerAppointmentSummaryResponse(
    int Total,
    int Pending,
    int Completed);

public sealed record CustomerAppointmentsPageMetadataResponse(
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);

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
    bool HasReview,
    CustomerAppointmentBusinessPhotoResponse? BusinessMainPhoto,
    decimal? ReviewRating,
    bool CanCancel,
    bool CanReview);

public sealed record CustomerAppointmentBusinessPhotoResponse(
    string ImageUrl,
    string AltText);

internal sealed record CustomerAppointmentListRow(
    Guid Id,
    AppointmentStatus Status,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    string BusinessName,
    string ServiceName,
    string? StaffFirstName,
    string? StaffLastName,
    decimal PriceAmount,
    string CurrencyCode,
    decimal? ReviewRating,
    string? BusinessPhotoImageUrl,
    string? BusinessPhotoAltText);

internal enum CustomerAppointmentView
{
    All,
    Upcoming,
    Completed
}

internal enum CustomerAppointmentSort
{
    DateAscending,
    DateDescending
}

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
