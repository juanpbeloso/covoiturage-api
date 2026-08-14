using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SubiteAPI.Data;
using SubiteAPI.DTOs;
using SubiteAPI.Exceptions;
using SubiteAPI.Helpers;
using SubiteAPI.Models;
using SubiteAPI.Options;
using MpPayment = MercadoPago.Resource.Payment.Payment;

namespace SubiteAPI.Services;

public class PaymentService : IPaymentService
{
    private readonly AppDbContext _db;
    private readonly IReservationService _reservationService;
    private readonly IMercadoPagoService _mercadoPago;
    private readonly INotificationService _notifications;
    private readonly IPlatformSettingsService _platformSettings;
    private readonly IMercadoPagoTokenService _mpTokens;
    private readonly MercadoPagoOptions _mpOptions;
    private readonly AppOptions _appOptions;

    public PaymentService(
        AppDbContext db,
        IReservationService reservationService,
        IMercadoPagoService mercadoPago,
        INotificationService notifications,
        IPlatformSettingsService platformSettings,
        IMercadoPagoTokenService mpTokens,
        IOptions<MercadoPagoOptions> mpOptions,
        IOptions<AppOptions> appOptions)
    {
        _db = db;
        _reservationService = reservationService;
        _mercadoPago = mercadoPago;
        _notifications = notifications;
        _platformSettings = platformSettings;
        _mpTokens = mpTokens;
        _mpOptions = mpOptions.Value;
        _appOptions = appOptions.Value;
    }

    public async Task<CheckoutPaymentResultDto> CheckoutAsync(Guid passengerId, CheckoutPaymentDto dto)
    {
        await ExpireStalePendingCheckoutsAsync().ConfigureAwait(false);

        // Mobile: back_urls como deep links de la app (subite://...).
        // HTTPS (ngrok) solo para webhook. Usar HTTPS en back_urls abre Safari y
        // luego subite://payments/... que Expo Router no tenía mapeado → "unmatched route".
        var appBase = string.IsNullOrWhiteSpace(_appOptions.FrontendUrl)
            ? "subite://"
            : _appOptions.FrontendUrl;
        var successUrl = PaymentUrlHelper.BuildAppReturnUrl(appBase, "payments/return/success");
        var failureUrl = PaymentUrlHelper.BuildAppReturnUrl(appBase, "payments/return/failure");
        var pendingUrl = PaymentUrlHelper.BuildAppReturnUrl(appBase, "payments/return/pending");

        // Si el cliente pide retorno web explícito (HTTPS), respetarlo (flujo browser).
        if (PaymentUrlHelper.IsHttpsUrl(dto.ReturnBaseUrl))
        {
            var webBase = dto.ReturnBaseUrl!.TrimEnd('/');
            successUrl = $"{webBase}/api/payments/return/success";
            failureUrl = $"{webBase}/api/payments/return/failure";
            pendingUrl = $"{webBase}/api/payments/return/pending";
        }

        var notificationUrl = $"{_appOptions.BackendUrl.TrimEnd('/')}/api/payments/webhook";

        Guid? createdReservationId = null;

        try
        {
            var reservationDto = await _reservationService.CreateAsync(
                passengerId,
                new CreateReservationDto
                {
                    RideId = dto.RideId,
                    SeatsReserved = dto.SeatsReserved,
                    BoardingCity = dto.BoardingCity,
                    AlightingCity = dto.AlightingCity
                },
                notifyDriver: false).ConfigureAwait(false);

            createdReservationId = reservationDto.Id;

            var reservation = await _db.Reservations
                .Include(r => r.Ride)
                .Include(r => r.Passenger)
                .FirstAsync(r => r.Id == reservationDto.Id)
                .ConfigureAwait(false);

            var sellerToken = await _mpTokens
                .GetValidAccessTokenAsync(reservation.Ride.DriverId)
                .ConfigureAwait(false);

            var baseAmount = reservation.TotalPrice;
            var commissionRate = await _platformSettings.GetCommissionRateAsync().ConfigureAwait(false);
            var commission = Math.Round(baseAmount * commissionRate, 0);
            // Split 1:1: el pasajero paga base+comisión; marketplace_fee = comisión de Subite.
            var totalAmount = baseAmount + commission;

            var preference = await _mercadoPago.CreateCheckoutPreferenceAsync(
                reservation,
                reservation.Ride,
                reservation.Passenger,
                totalAmount,
                commission,
                sellerToken,
                successUrl,
                failureUrl,
                pendingUrl,
                notificationUrl).ConfigureAwait(false);

            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                ReservationId = reservation.Id,
                MercadoPagoPreferenceId = preference.Id,
                Amount = totalAmount,
                Status = PaymentStatus.Pending,
                PaymentMethod = "mercadopago_checkout_pro",
                CreatedAt = DateTime.UtcNow
            };

            _db.Payments.Add(payment);
            await _db.SaveChangesAsync().ConfigureAwait(false);

            createdReservationId = null;

            return new CheckoutPaymentResultDto
            {
                ReservationId = reservation.Id,
                PreferenceId = preference.Id ?? string.Empty,
                InitPoint = preference.InitPoint ?? string.Empty,
                SandboxInitPoint = preference.SandboxInitPoint,
                IsSandbox = IsSandboxToken(),
                BaseAmount = baseAmount,
                CommissionAmount = commission,
                Amount = totalAmount
            };
        }
        catch
        {
            await RollbackReservationIfCreatedAsync(passengerId, createdReservationId).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<PaymentStatusDto> GetStatusAsync(Guid userId, Guid reservationId)
    {
        await ExpireStalePendingCheckoutsAsync().ConfigureAwait(false);
        var payment = await GetAuthorizedPaymentAsync(userId, reservationId).ConfigureAwait(false);
        return MapStatus(payment);
    }

    public async Task<PaymentStatusDto> SyncStatusAsync(Guid userId, Guid reservationId)
    {
        await ExpireStalePendingCheckoutsAsync().ConfigureAwait(false);
        var payment = await GetAuthorizedPaymentAsync(userId, reservationId).ConfigureAwait(false);

        if (payment.Status == PaymentStatus.Approved)
        {
            return MapStatus(payment);
        }

        var mpPayment = await _mercadoPago
            .FindPaymentByExternalReferenceAsync(reservationId.ToString())
            .ConfigureAwait(false);

        if (mpPayment != null)
        {
            ValidatePaymentMethod(mpPayment);
            await ApplyMercadoPagoPaymentAsync(payment, mpPayment).ConfigureAwait(false);
        }

        return MapStatus(payment);
    }

    public async Task<PaymentStatusDto> AbandonCheckoutAsync(Guid userId, Guid reservationId)
    {
        var payment = await GetAuthorizedPaymentAsync(userId, reservationId).ConfigureAwait(false);

        if (payment.Status == PaymentStatus.Approved)
        {
            return MapStatus(payment);
        }

        var mpPayment = await _mercadoPago
            .FindPaymentByExternalReferenceAsync(reservationId.ToString())
            .ConfigureAwait(false);

        if (mpPayment != null)
        {
            ValidatePaymentMethod(mpPayment);
            await ApplyMercadoPagoPaymentAsync(payment, mpPayment).ConfigureAwait(false);
            if (payment.Status == PaymentStatus.Approved)
            {
                return MapStatus(payment);
            }
        }

        if (payment.Reservation.Status == ReservationStatus.Pending)
        {
            await ReleaseSeatsAsync(payment.Reservation).ConfigureAwait(false);
            payment.Reservation.Status = ReservationStatus.Cancelled;
            payment.Reservation.CancelledAt = DateTime.UtcNow;
            payment.Reservation.CancellationReason = "Pago no completado";
        }

        if (payment.Status == PaymentStatus.Pending)
        {
            payment.Status = PaymentStatus.Cancelled;
            payment.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync().ConfigureAwait(false);
        return MapStatus(payment);
    }

    public async Task ProcessWebhookPaymentAsync(long mercadoPagoPaymentId)
    {
        var mpPayment = await _mercadoPago.GetPaymentAsync(mercadoPagoPaymentId).ConfigureAwait(false);
        if (mpPayment == null || string.IsNullOrWhiteSpace(mpPayment.ExternalReference))
        {
            return;
        }

        if (!Guid.TryParse(mpPayment.ExternalReference, out var reservationId))
        {
            return;
        }

        var payment = await _db.Payments
            .Include(p => p.Reservation)
            .FirstOrDefaultAsync(p => p.ReservationId == reservationId)
            .ConfigureAwait(false);

        if (payment == null)
        {
            return;
        }

        ValidatePaymentMethod(mpPayment);
        await ApplyMercadoPagoPaymentAsync(payment, mpPayment).ConfigureAwait(false);
    }

    public async Task<int> ExpireStalePendingCheckoutsAsync()
    {
        var holdMinutes = Math.Max(1, _mpOptions.CheckoutHoldMinutes);
        var cutoff = DateTime.UtcNow.AddMinutes(-holdMinutes);

        var stale = await _db.Payments
            .Include(p => p.Reservation)
                .ThenInclude(r => r.Ride)
            .Where(p =>
                p.Status == PaymentStatus.Pending &&
                p.Reservation.Status == ReservationStatus.Pending &&
                p.CreatedAt < cutoff)
            .ToListAsync()
            .ConfigureAwait(false);

        if (stale.Count == 0)
        {
            return 0;
        }

        foreach (var payment in stale)
        {
            await ReleaseSeatsAsync(payment.Reservation).ConfigureAwait(false);
            payment.Reservation.Status = ReservationStatus.Cancelled;
            payment.Reservation.CancelledAt = DateTime.UtcNow;
            payment.Reservation.CancellationReason =
                $"Pago no completado en {holdMinutes} minutos";
            payment.Status = PaymentStatus.Cancelled;
            payment.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync().ConfigureAwait(false);
        return stale.Count;
    }

    private async Task RollbackReservationIfCreatedAsync(Guid passengerId, Guid? reservationId)
    {
        if (reservationId == null)
        {
            return;
        }

        try
        {
            await _reservationService.CancelAsync(
                passengerId,
                reservationId.Value,
                "No se pudo iniciar el checkout de Mercado Pago",
                notify: false).ConfigureAwait(false);
        }
        catch
        {
            // Si el rollback falla, la excepción original del checkout sigue siendo la relevante.
        }
    }

    private string ResolveReturnBaseOrThrow(string? clientReturnBase)
    {
        try
        {
            return PaymentUrlHelper.ResolveReturnBase(clientReturnBase, _appOptions.BackendUrl);
        }
        catch (InvalidOperationException ex)
        {
            throw new BusinessException("MP_005", ex.Message);
        }
    }

    private async Task<Payment> GetAuthorizedPaymentAsync(Guid userId, Guid reservationId)
    {
        var payment = await _db.Payments
            .Include(p => p.Reservation)
            .FirstOrDefaultAsync(p => p.ReservationId == reservationId)
            .ConfigureAwait(false)
            ?? throw new BusinessException("MP_002", "Pago no encontrado para esta reserva.", 404);

        if (payment.Reservation.PassengerId != userId)
        {
            throw new BusinessException("MP_003", "No tenés permiso para ver este pago.", 403);
        }

        return payment;
    }

    private void ValidatePaymentMethod(MpPayment mpPayment)
    {
        if (!_mpOptions.PaymentMode.Equals("wallet_only", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var methodId = mpPayment.PaymentMethodId ?? mpPayment.PaymentTypeId ?? string.Empty;
        if (!methodId.Equals("account_money", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException(
                "MP_004",
                "Solo se aceptan pagos con dinero en cuenta de Mercado Pago.");
        }
    }

    private async Task ApplyMercadoPagoPaymentAsync(Payment payment, MpPayment mpPayment)
    {
        var previousStatus = payment.Status;

        payment.MercadoPagoPaymentId = mpPayment.Id?.ToString();
        payment.Status = MapMercadoPagoStatus(mpPayment.Status);
        payment.PaymentMethod = mpPayment.PaymentMethodId ?? payment.PaymentMethod;
        payment.UpdatedAt = DateTime.UtcNow;

        var justApproved = previousStatus != PaymentStatus.Approved &&
                           payment.Status == PaymentStatus.Approved;

        if (payment.Status == PaymentStatus.Approved &&
            payment.Reservation.Status == ReservationStatus.Pending)
        {
            payment.Reservation.Status = ReservationStatus.Confirmed;
            payment.Reservation.ConfirmedAt = DateTime.UtcNow;
        }

        if (payment.Status is PaymentStatus.Rejected or PaymentStatus.Cancelled &&
            payment.Reservation.Status == ReservationStatus.Pending)
        {
            await ReleaseSeatsAsync(payment.Reservation).ConfigureAwait(false);
            payment.Reservation.Status = ReservationStatus.Cancelled;
            payment.Reservation.CancelledAt = DateTime.UtcNow;
            payment.Reservation.CancellationReason = "Pago rechazado o cancelado";
        }

        await _db.SaveChangesAsync().ConfigureAwait(false);

        if (justApproved)
        {
            await _notifications.NotifyUserAsync(
                payment.Reservation.PassengerId,
                "payment",
                "Pago aprobado",
                "Tu pago con Mercado Pago se acreditó y la reserva quedó confirmada.",
                actionUrl: $"/rides/reservation-detail?id={payment.ReservationId}",
                data: new { reservationId = payment.ReservationId, paymentId = payment.Id })
                .ConfigureAwait(false);

            var ride = await _db.Rides
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == payment.Reservation.RideId)
                .ConfigureAwait(false);

            var passenger = await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == payment.Reservation.PassengerId)
                .ConfigureAwait(false);

            if (ride != null)
            {
                var passengerName = passenger?.FullName ?? "Un pasajero";
                var seats = payment.Reservation.SeatsReserved;
                await _notifications.NotifyUserAsync(
                    ride.DriverId,
                    "booking",
                    "Nueva reserva",
                    $"{passengerName} reservó {seats} asiento(s) en tu viaje {ride.OriginCity} → {ride.DestinationCity} (pago confirmado).",
                    actionUrl: $"/rides/published-ride-detail?rideId={ride.Id}",
                    data: new { reservationId = payment.ReservationId, rideId = ride.Id, paymentId = payment.Id })
                    .ConfigureAwait(false);
            }
        }
    }

    private async Task ReleaseSeatsAsync(Reservation reservation)
    {
        var ride = await _db.Rides.FirstAsync(r => r.Id == reservation.RideId).ConfigureAwait(false);
        ride.AvailableSeats += reservation.SeatsReserved;
        if (ride.Status == RideStatus.Full &&
            ride.AvailableSeats > 0 &&
            ride.DepartureDateTime > DateTime.UtcNow)
        {
            ride.Status = RideStatus.Active;
        }
    }

    private static PaymentStatus MapMercadoPagoStatus(string? status) => status switch
    {
        "approved" => PaymentStatus.Approved,
        "rejected" => PaymentStatus.Rejected,
        "cancelled" => PaymentStatus.Cancelled,
        "refunded" => PaymentStatus.Refunded,
        _ => PaymentStatus.Pending
    };

    private PaymentStatusDto MapStatus(Payment payment)
    {
        DateTime? expiresAt = null;
        int? secondsRemaining = null;

        if (payment.Status == PaymentStatus.Pending &&
            payment.Reservation.Status == ReservationStatus.Pending)
        {
            var holdMinutes = Math.Max(1, _mpOptions.CheckoutHoldMinutes);
            expiresAt = payment.CreatedAt.AddMinutes(holdMinutes);
            secondsRemaining = Math.Max(
                0,
                (int)Math.Ceiling((expiresAt.Value - DateTime.UtcNow).TotalSeconds));
        }

        return new PaymentStatusDto
        {
            ReservationId = payment.ReservationId,
            PaymentStatus = ToSpanishPaymentStatus(payment.Status),
            PaymentStatusCode = payment.Status.ToString(),
            ReservationStatus = payment.Reservation.Status.ToString(),
            IsApproved = payment.Status == PaymentStatus.Approved,
            CheckoutExpiresAt = expiresAt,
            SecondsRemaining = secondsRemaining
        };
    }

    private static string ToSpanishPaymentStatus(PaymentStatus status) => status switch
    {
        PaymentStatus.Pending => "Pendiente",
        PaymentStatus.Approved => "Aprobado",
        PaymentStatus.Rejected => "Rechazado",
        PaymentStatus.Cancelled => "Cancelado",
        PaymentStatus.Refunded => "Reembolsado",
        _ => status.ToString()
    };

    private bool IsSandboxToken() =>
        _mpOptions.AccessToken.StartsWith("TEST-", StringComparison.OrdinalIgnoreCase);
}
