using MercadoPago.Client.Payment;
using MercadoPago.Client.Preference;
using MercadoPago.Config;
using MercadoPago.Resource.Preference;
using Microsoft.Extensions.Options;
using SubiteAPI.Exceptions;
using SubiteAPI.Helpers;
using SubiteAPI.Models;
using SubiteAPI.Options;
using MpPayment = MercadoPago.Resource.Payment.Payment;
using MpSearchRequest = MercadoPago.Client.SearchRequest;

namespace SubiteAPI.Services;

public class MercadoPagoService : IMercadoPagoService
{
    private readonly MercadoPagoOptions _options;

    public MercadoPagoService(IOptions<MercadoPagoOptions> options)
    {
        _options = options.Value;
        if (IsConfigured)
        {
            MercadoPagoConfig.AccessToken = _options.AccessToken;
        }
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.AccessToken) &&
        !_options.AccessToken.StartsWith("TEST-xxxx", StringComparison.OrdinalIgnoreCase);

    public async Task<Preference> CreateCheckoutPreferenceAsync(
        Reservation reservation,
        Ride ride,
        User passenger,
        decimal totalAmount,
        string successUrl,
        string failureUrl,
        string pendingUrl,
        string notificationUrl)
    {
        EnsureConfigured();

        var client = new PreferenceClient();
        var title = $"Viaje: {ride.OriginCity} → {ride.DestinationCity}";

        var request = new PreferenceRequest
        {
            Items =
            [
                new PreferenceItemRequest
                {
                    Id = reservation.Id.ToString(),
                    Title = title,
                    Description = $"Salida {ride.DepartureDateTime:dd/MM/yyyy HH:mm} · {reservation.SeatsReserved} asiento(s)",
                    Quantity = 1,
                    CurrencyId = "ARS",
                    UnitPrice = totalAmount
                }
            ],
            Payer = new PreferencePayerRequest
            {
                Email = passenger.Email,
                Name = passenger.FullName
            },
            ExternalReference = reservation.Id.ToString(),
            // Webhook IPN: MP exige HTTPS público; en local se omite y se usa sync/polling.
            NotificationUrl = PaymentUrlHelper.IsHttpsUrl(notificationUrl) ? notificationUrl : null,
            StatementDescriptor = "SUBITE"
        };

        if ((PaymentUrlHelper.IsHttpsUrl(successUrl) || PaymentUrlHelper.IsAppDeepLink(successUrl)) &&
            (PaymentUrlHelper.IsHttpsUrl(failureUrl) || PaymentUrlHelper.IsAppDeepLink(failureUrl)) &&
            (PaymentUrlHelper.IsHttpsUrl(pendingUrl) || PaymentUrlHelper.IsAppDeepLink(pendingUrl)))
        {
            request.BackUrls = new PreferenceBackUrlsRequest
            {
                Success = successUrl,
                Failure = failureUrl,
                Pending = pendingUrl
            };

            request.AutoReturn = "approved";
        }

        if (IsWalletOnlyMode())
        {
            request.Purpose = "wallet_purchase";
            request.PaymentMethods = new PreferencePaymentMethodsRequest
            {
                ExcludedPaymentTypes =
                [
                    new PreferencePaymentTypeRequest { Id = "credit_card" },
                    new PreferencePaymentTypeRequest { Id = "debit_card" },
                    new PreferencePaymentTypeRequest { Id = "ticket" },
                    new PreferencePaymentTypeRequest { Id = "bank_transfer" },
                    new PreferencePaymentTypeRequest { Id = "atm" },
                    new PreferencePaymentTypeRequest { Id = "prepaid_card" },
                ],
                Installments = 1
            };
        }

        try
        {
            return await client.CreateAsync(request).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new MercadoPagoException("No se pudo crear la preferencia de pago.", ex);
        }
    }

    public async Task<MpPayment?> GetPaymentAsync(long paymentId)
    {
        EnsureConfigured();
        try
        {
            var client = new PaymentClient();
            return await client.GetAsync(paymentId).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new MercadoPagoException($"No se pudo consultar el pago {paymentId}.", ex);
        }
    }

    public async Task<MpPayment?> FindPaymentByExternalReferenceAsync(string externalReference)
    {
        EnsureConfigured();
        try
        {
            var client = new PaymentClient();
            var search = new MpSearchRequest
            {
                Limit = 1,
                Filters = new Dictionary<string, object>
                {
                    ["external_reference"] = externalReference
                }
            };

            var results = await client.SearchAsync(search).ConfigureAwait(false);
            return results?.Results?.FirstOrDefault();
        }
        catch (Exception ex)
        {
            throw new MercadoPagoException("No se pudo buscar pagos por referencia externa.", ex);
        }
    }

    private void EnsureConfigured()
    {
        if (!IsConfigured)
        {
            throw new BusinessException(
                "MP_001",
                "MercadoPago no está configurado. Agregá tu Access Token de prueba en appsettings.");
        }
    }

    private bool IsWalletOnlyMode() =>
        string.Equals(_options.PaymentMode, "wallet_only", StringComparison.OrdinalIgnoreCase);
}
