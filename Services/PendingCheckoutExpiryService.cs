namespace SubiteAPI.Services;

/// <summary>
/// Libera periódicamente asientos de checkouts Pending que superaron el hold (ej. 5 min).
/// </summary>
public class PendingCheckoutExpiryService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PendingCheckoutExpiryService> _logger;

    public PendingCheckoutExpiryService(
        IServiceScopeFactory scopeFactory,
        ILogger<PendingCheckoutExpiryService> logger)
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
                var payments = scope.ServiceProvider.GetRequiredService<IPaymentService>();
                var expired = await payments.ExpireStalePendingCheckoutsAsync().ConfigureAwait(false);
                if (expired > 0)
                {
                    _logger.LogInformation(
                        "Expiraron {Count} checkout(s) pendientes sin pago.",
                        expired);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al expirar checkouts pendientes.");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken).ConfigureAwait(false);
        }
    }
}
