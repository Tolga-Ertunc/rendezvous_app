using Microsoft.EntityFrameworkCore;
using Rendezvous.Domain.Appointments;
using Rendezvous.Infrastructure.Persistence;

namespace Rendezvous.Api.Services;

public class AppointmentExpirationService
{
    private readonly AppDbContext dbContext;

    public AppointmentExpirationService(AppDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<int> ExpirePendingAppointmentsAsync(CancellationToken cancellationToken)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var appointments = await dbContext.Appointments
            .Where(appointment =>
                appointment.Status == AppointmentStatus.Pending
                && appointment.StartsAtUtc <= nowUtc)
            .ToListAsync(cancellationToken);

        foreach (var appointment in appointments)
        {
            appointment.Status = AppointmentStatus.Expired;
        }

        if (appointments.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return appointments.Count;
    }
}
