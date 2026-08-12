namespace SubiteAPI.Options;

public class MercadoPagoOptions
{
    public const string SectionName = "MercadoPago";

    public string AccessToken { get; set; } = string.Empty;
    public string? WebhookSecret { get; set; }
    public bool UseSandbox { get; set; } = true;
    public decimal PlatformCommissionRate { get; set; } = 0.125m;

    /// <summary>
    /// "wallet_only" = solo dinero en cuenta MP (purpose: wallet_purchase).
    /// "all" = todos los medios habilitados en tu cuenta MP.
    /// </summary>
    public string PaymentMode { get; set; } = "wallet_only";

    /// <summary>
    /// Minutos que se retiene el asiento con pago pendiente. Luego se libera.
    /// </summary>
    public int CheckoutHoldMinutes { get; set; } = 5;
}

public class AppOptions
{
    public const string SectionName = "App";

    public string BackendUrl { get; set; } = "http://localhost:5178";
    public string FrontendUrl { get; set; } = "subite://";
}
