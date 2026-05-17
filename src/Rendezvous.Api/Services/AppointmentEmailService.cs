using Microsoft.EntityFrameworkCore;
using Rendezvous.Api.Email;
using Rendezvous.Infrastructure.Identity;
using Rendezvous.Infrastructure.Persistence;

namespace Rendezvous.Api.Services;

public class AppointmentEmailService
{
    private readonly AppDbContext dbContext;
    private readonly IEmailSender emailSender;
    private readonly ILogger<AppointmentEmailService> logger;

    public AppointmentEmailService(
        AppDbContext dbContext,
        IEmailSender emailSender,
        ILogger<AppointmentEmailService> logger)
    {
        this.dbContext = dbContext;
        this.emailSender = emailSender;
        this.logger = logger;
    }

    public async Task SendApprovalEmailAsync(
        Guid appointmentId,
        CancellationToken cancellationToken)
    {
        var row = await dbContext.Appointments
            .AsNoTracking()
            .Where(appointment => appointment.Id == appointmentId)
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
            .SingleOrDefaultAsync(cancellationToken);

        if (row?.user.Email is null || !row.user.EmailConfirmed)
        {
            return;
        }

        var startsAtLocal = ConvertToBusinessTime(
            row.appointment.StartsAtUtc,
            row.business.TimeZoneId);
        var staffDisplayName = UserNames.FormatFullName(row.staffUser.FirstName, row.staffUser.LastName);
        var body = $"""
            Your appointment is approved.

            Business: {row.business.Name}
            Service: {row.service.Name}
            Staff: {staffDisplayName}
            Date/time: {startsAtLocal:yyyy-MM-dd HH:mm} ({row.business.TimeZoneId})
            Price: {row.appointment.PriceAmount:0.##} {row.appointment.CurrencyCode}

            You can review your appointments in Rendezvous.
            """;

        try
        {
            await emailSender.SendAsync(
                new EmailMessage(
                    row.user.Email,
                    "Your appointment is approved",
                    body),
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Appointment approval email failed for appointment {AppointmentId}.",
                appointmentId);
        }
    }

    private static DateTimeOffset ConvertToBusinessTime(
        DateTimeOffset utcDateTime,
        string timeZoneId)
    {
        try
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return TimeZoneInfo.ConvertTime(utcDateTime, timeZone);
        }
        catch (TimeZoneNotFoundException)
        {
            return utcDateTime;
        }
        catch (InvalidTimeZoneException)
        {
            return utcDateTime;
        }
    }
}
