using Microsoft.EntityFrameworkCore;
using Rendezvous.Domain.Appointments;
using Rendezvous.Infrastructure.Persistence;

namespace Rendezvous.Api.Services;

public class AppointmentLifecycleService
{
    private readonly AppDbContext dbContext;
    private readonly AppointmentNotificationService notificationService;

    public AppointmentLifecycleService(
        AppDbContext dbContext,
        AppointmentNotificationService notificationService)
    {
        this.dbContext = dbContext;
        this.notificationService = notificationService;
    }

    public async Task<AppointmentLifecycleResult> ProcessDueAppointmentsAsync(CancellationToken cancellationToken)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var automaticCompletionCutoffUtc = nowUtc - Appointment.AutomaticCompletionDelay;
        var expiredCount = 0;
        var completedCount = 0;

        var pendingAppointments = await dbContext.Appointments
            .Where(appointment =>
                appointment.Status == AppointmentStatus.Pending
                && appointment.StartsAtUtc <= nowUtc)
            .ToListAsync(cancellationToken);

        foreach (var appointment in pendingAppointments)
        {
            if (appointment.ExpirePending(nowUtc))
            {
                notificationService.AddCustomerAppointmentExpired(appointment);
                expiredCount++;
            }
        }

        var approvedAppointments = await dbContext.Appointments
            .Where(appointment =>
                appointment.Status == AppointmentStatus.Approved
                && appointment.EndsAtUtc <= automaticCompletionCutoffUtc)
            .ToListAsync(cancellationToken);

        foreach (var appointment in approvedAppointments)
        {
            if (appointment.CompleteApprovedAppointment(nowUtc, automatic: true))
            {
                completedCount++;
            }
        }

        if (expiredCount > 0 || completedCount > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new AppointmentLifecycleResult(expiredCount, completedCount);
    }
}

public sealed record AppointmentLifecycleResult(
    int ExpiredCount,
    int CompletedCount);
