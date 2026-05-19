using Microsoft.EntityFrameworkCore;
using Rendezvous.Domain.Appointments;
using Rendezvous.Domain.Businesses;
using Rendezvous.Domain.Notifications;
using Rendezvous.Infrastructure.Persistence;

namespace Rendezvous.Api.Services;

public class AppointmentNotificationService
{
    private readonly AppDbContext dbContext;
    private readonly NotificationWriter notificationWriter;

    public AppointmentNotificationService(
        AppDbContext dbContext,
        NotificationWriter notificationWriter)
    {
        this.dbContext = dbContext;
        this.notificationWriter = notificationWriter;
    }

    public void AddCustomerAppointmentCancelled(Appointment appointment)
    {
        notificationWriter.Add(
            appointment.CustomerUserId,
            "Appointment cancelled",
            "Your appointment was cancelled.",
            NotificationType.AppointmentCancelled,
            "/appointments");
    }

    public void AddCustomerAppointmentExpired(Appointment appointment)
    {
        notificationWriter.Add(
            appointment.CustomerUserId,
            "Appointment expired",
            "Your appointment request expired because it was not approved in time.",
            NotificationType.AppointmentExpired,
            "/appointments");
    }

    public void AddCustomerAppointmentNoShow(Appointment appointment)
    {
        notificationWriter.Add(
            appointment.CustomerUserId,
            "Appointment marked no-show",
            "Your appointment was marked as no-show.",
            NotificationType.AppointmentNoShow,
            "/appointments");
    }

    public async Task AddBusinessAppointmentCancelledByCustomerAsync(
        Appointment appointment,
        CancellationToken cancellationToken)
    {
        var business = await dbContext.Businesses
            .AsNoTracking()
            .Where(candidate => candidate.Id == appointment.BusinessId)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.Name
            })
            .SingleAsync(cancellationToken);

        var ownerUserIds = await dbContext.BusinessMemberships
            .AsNoTracking()
            .Where(membership =>
                membership.BusinessId == appointment.BusinessId
                && membership.Role == BusinessMembershipRole.Owner
                && membership.Status == BusinessMembershipStatus.Active)
            .Select(membership => membership.UserId)
            .ToListAsync(cancellationToken);

        var staffUserId = await dbContext.StaffMembers
            .AsNoTracking()
            .Where(staffMember => staffMember.Id == appointment.StaffMemberId)
            .Select(staffMember => staffMember.UserId)
            .SingleAsync(cancellationToken);

        foreach (var ownerUserId in ownerUserIds)
        {
            notificationWriter.Add(
                ownerUserId,
                "Appointment cancelled",
                $"A customer cancelled an appointment for {business.Name}.",
                NotificationType.AppointmentCancelled,
                $"/owner/businesses/{business.Id}/appointments");
        }

        if (!ownerUserIds.Contains(staffUserId))
        {
            notificationWriter.Add(
                staffUserId,
                "Appointment cancelled",
                $"A customer cancelled an appointment for {business.Name}.",
                NotificationType.AppointmentCancelled,
                "/employee/appointments");
        }
    }
}
