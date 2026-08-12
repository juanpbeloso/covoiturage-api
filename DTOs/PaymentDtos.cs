namespace SubiteAPI.DTOs;

public class CheckoutPaymentDto
{
    public Guid RideId { get; set; }
    public int SeatsReserved { get; set; } = 1;
    public string? BoardingCity { get; set; }
    public string? AlightingCity { get; set; }

    /// <summary>
    /// Base http(s) desde la app (ej. http://192.168.0.10:5178) para back_urls de MP.
    /// </summary>
    public string? ReturnBaseUrl { get; set; }
}

public class CheckoutPaymentResultDto
{
    public Guid ReservationId { get; set; }
    public string PreferenceId { get; set; } = string.Empty;
    public string InitPoint { get; set; } = string.Empty;
    public string? SandboxInitPoint { get; set; }
    public bool IsSandbox { get; set; }
    public decimal BaseAmount { get; set; }
    public decimal CommissionAmount { get; set; }
    public decimal Amount { get; set; }
}

public class PaymentStatusDto
{
    public Guid ReservationId { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
    /// <summary>Código estable en inglés: Pending, Approved, Rejected, Cancelled, Refunded.</summary>
    public string PaymentStatusCode { get; set; } = string.Empty;
    public string ReservationStatus { get; set; } = string.Empty;
    public bool IsApproved { get; set; }
    /// <summary>UTC en que vence el hold del checkout (solo si sigue Pending).</summary>
    public DateTime? CheckoutExpiresAt { get; set; }
    public int? SecondsRemaining { get; set; }
}

public class MercadoPagoWebhookNotificationDto
{
    public string? Action { get; set; }
    public string? Type { get; set; }
    public MercadoPagoWebhookDataDto? Data { get; set; }
}

public class MercadoPagoWebhookDataDto
{
    public string? Id { get; set; }
}
