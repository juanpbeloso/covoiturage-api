namespace SubiteAPI.Options;

public class MercadoPagoOptions
{
    public const string SectionName = "MercadoPago";

    /// <summary>Access token de la aplicación marketplace (Subite). Se usa para OAuth app y consultas de pago.</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>Client ID / Application ID de la app marketplace en developers.mercadopago.com.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Client Secret de la app marketplace.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Redirect URI registrada en MP (debe coincidir exacto).
    /// Ej: https://api.subite.../api/conductores/mercadopago/callback
    /// </summary>
    public string RedirectUri { get; set; } = string.Empty;

    public string? WebhookSecret { get; set; }
    public bool UseSandbox { get; set; } = true;

    /// <summary>Fallback si no hay PlatformSettings en DB. Preferir /admin/settings.</summary>
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

    /// <summary>Minutos antes del vencimiento para renovar el token del conductor.</summary>
    public int TokenRefreshSkewMinutes { get; set; } = 10;
}

public class AppOptions
{
    public const string SectionName = "App";

    public string BackendUrl { get; set; } = "http://localhost:5178";
    public string FrontendUrl { get; set; } = "subite://";
}
