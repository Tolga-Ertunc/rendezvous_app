using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rendezvous.Api.Services;
using Rendezvous.Infrastructure.Persistence;

namespace Rendezvous.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/customer/appointments")]
public class CustomerAppointmentsController : ControllerBase
{
    private readonly AppDbContext dbContext;
    private readonly AppointmentExpirationService expirationService;

    public CustomerAppointmentsController(
        AppDbContext dbContext,
        AppointmentExpirationService expirationService)
    {
        this.dbContext = dbContext;
        this.expirationService = expirationService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CustomerAppointmentResponse>>> List(
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        await expirationService.ExpirePendingAppointmentsAsync(cancellationToken);

        return await dbContext.Appointments
            .AsNoTracking()
            .Where(appointment => appointment.CustomerUserId == userId.Value)
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
            .OrderBy(row => row.appointment.StartsAtUtc)
            .Select(row => new CustomerAppointmentResponse(
                row.appointment.Id,
                row.appointment.Status.ToString(),
                row.appointment.StartsAtUtc,
                row.appointment.EndsAtUtc,
                row.business.Name,
                row.service.Name,
                row.staffMember.DisplayName,
                row.appointment.PriceAmount,
                row.appointment.CurrencyCode))
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

        await dbContext.SaveChangesAsync(cancellationToken);

        return new CustomerAppointmentDecisionResponse(
            appointment.Id,
            appointment.Status.ToString());
    }

    private Guid? GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(userIdValue, out var userId)
            ? userId
            : null;
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
    string CurrencyCode);

public sealed record CustomerAppointmentDecisionResponse(
    Guid Id,
    string Status);
