namespace SubiteAPI.Services;

/// <summary>
/// Pasa viajes Active/Full a InProgress al salir y a Completed al llegar.
/// </summary>
public class RideLifecycleService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RideLifecycleService> _logger;

    public RideLifecycleService(
        IServiceScopeFactory scopeFactory,
        ILogger<RideLifecycleService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var rides = scope.ServiceProvider.GetRequiredService<IRideService>();
                var advanced = await rides.AdvanceStaleRideStatusesAsync().ConfigureAwait(false);
                if (advanced > 0)
                {
                    _logger.LogInformation("Se actualizó el estado de {Count} viaje(s).", advanced);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al actualizar el ciclo de vida de los viajes.");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken).ConfigureAwait(false);
        }
    }
}
