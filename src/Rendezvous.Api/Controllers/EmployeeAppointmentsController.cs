using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rendezvous.Domain.Appointments;
using Rendezvous.Domain.Businesses;
using Rendezvous.Infrastructure.Persistence;

namespace Rendezvous.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/employee/appointments")]
public class EmployeeAppointmentsController : ControllerBase
{
    private readonly AppDbContext dbContext;

    public EmployeeAppointmentsController(AppDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EmployeeAppointmentResponse>>> List(
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var nowUtc = DateTimeOffset.UtcNow;

        return await dbContext.Appointments
            .AsNoTracking()
            .Where(appointment =>
                appointment.Status == AppointmentStatus.Approved
                && appointment.StartsAtUtc >= nowUtc)
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
                (row, _) => row)
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
                row => row.appointment.CustomerUserId,
                user => user.Id,
                (row, user) => new { row.appointment, row.staffMember, row.business, row.service, user })
            .OrderBy(row => row.appointment.StartsAtUtc)
            .Select(row => new EmployeeAppointmentResponse(
                row.appointment.Id,
                row.appointment.Status.ToString(),
                row.appointment.StartsAtUtc,
                row.appointment.EndsAtUtc,
                row.business.Id,
                row.business.Name,
                row.service.Name,
                row.staffMember.DisplayName,
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

    private Guid? GetCurrentUserId()
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(userIdValue, out var userId)
            ? userId
            : null;
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
