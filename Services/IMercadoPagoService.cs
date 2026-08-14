using SubiteAPI.Models;
using MercadoPago.Resource.Preference;
using MpPayment = MercadoPago.Resource.Payment.Payment;

namespace SubiteAPI.Services;

public interface IMercadoPagoService
{
    bool IsConfigured { get; }

    /// <summary>
    /// Crea preferencia Checkout Pro con el AccessToken del conductor (vendedor)
    /// y marketplace_fee para Subite.
    /// </summary>
    Task<Preference> CreateCheckoutPreferenceAsync(
        Reservation reservation,
        Ride ride,
        User passenger,
        decimal totalAmount,
        decimal marketplaceFee,
        string sellerAccessToken,
        string successUrl,
        string failureUrl,
        string pendingUrl,
        string notificationUrl);

    Task<MpPayment?> GetPaymentAsync(long paymentId);
    Task<MpPayment?> FindPaymentByExternalReferenceAsync(string externalReference);
}
