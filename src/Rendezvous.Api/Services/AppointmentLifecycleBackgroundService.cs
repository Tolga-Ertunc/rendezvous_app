namespace Rendezvous.Api.Services;

public class AppointmentLifecycleBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger<AppointmentLifecycleBackgroundService> logger;

    public AppointmentLifecycleBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<AppointmentLifecycleBackgroundService> logger)
    {
        this.scopeFactory = scopeFactory;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var lifecycleService = scope.ServiceProvider.GetRequiredService<AppointmentLifecycleService>();
                var result = await lifecycleService.ProcessDueAppointmentsAsync(stoppingToken);

                if (result.ExpiredCount > 0 || result.CompletedCount > 0)
                {
                    logger.LogInformation(
                        "Appointment lifecycle processed {ExpiredCount} expired and {CompletedCount} completed appointments.",
                        result.ExpiredCount,
                        result.CompletedCount);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Appointment lifecycle processing failed.");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
