namespace SubiteAPI.Models;

public enum PaymentStatus
{
    Pending,
    Approved,
    Rejected,
    Refunded,
    Cancelled
}

public class Payment
{
    public Guid Id { get; set; }
    public Guid ReservationId { get; set; }
    public string? MercadoPagoPaymentId { get; set; }
    public string? MercadoPagoPreferenceId { get; set; }
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string? PaymentMethod { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navegación
    public Reservation Reservation { get; set; } = null!;
}
