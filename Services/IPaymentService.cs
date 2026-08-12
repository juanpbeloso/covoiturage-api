using SubiteAPI.DTOs;

namespace SubiteAPI.Services;

public interface IPaymentService
{
    Task<CheckoutPaymentResultDto> CheckoutAsync(Guid passengerId, CheckoutPaymentDto dto);
    Task<PaymentStatusDto> GetStatusAsync(Guid userId, Guid reservationId);
    Task<PaymentStatusDto> SyncStatusAsync(Guid userId, Guid reservationId);
    Task<PaymentStatusDto> AbandonCheckoutAsync(Guid userId, Guid reservationId);
    Task ProcessWebhookPaymentAsync(long mercadoPagoPaymentId);
    /// <summary>Libera asientos de checkouts Pending vencidos (hold de N minutos).</summary>
    Task<int> ExpireStalePendingCheckoutsAsync();
}
