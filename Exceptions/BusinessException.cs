namespace SubiteAPI.Exceptions;

/// <summary>
/// Excepción base para errores de negocio/dominio.
/// Estos errores son esperados y se devuelven al cliente con mensaje amigable.
/// </summary>
public class BusinessException : Exception
{
    public string Code { get; }
    public int StatusCode { get; }

    public BusinessException(string code, string message, int statusCode = 400) 
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }
}

// ========== AUTH EXCEPTIONS ==========

public class InvalidCredentialsException : BusinessException
{
    public InvalidCredentialsException() 
        : base("AUTH_001", "Credenciales inválidas", 401) { }
}

public class EmailAlreadyExistsException : BusinessException
{
    public EmailAlreadyExistsException(string email) 
        : base("AUTH_002", $"El email {email} ya está registrado", 409) { }
}

public class InvalidTokenException : BusinessException
{
    public InvalidTokenException(string provider = "OAuth") 
        : base("AUTH_003", $"Token de {provider} inválido", 401) { }
}

public class UserNotFoundException : BusinessException
{
    public UserNotFoundException(string identifier) 
        : base("AUTH_004", $"Usuario no encontrado: {identifier}", 404) { }
}

// ========== RIDE EXCEPTIONS ==========

public class RideNotFoundException : BusinessException
{
    public RideNotFoundException(Guid rideId) 
        : base("RIDE_001", $"Viaje no encontrado: {rideId}", 404) { }
}

public class RideFullException : BusinessException
{
    public RideFullException(Guid rideId) 
        : base("RIDE_002", "El viaje ya no tiene asientos disponibles", 409) { }
}

public class RideAlreadyDepartedException : BusinessException
{
    public RideAlreadyDepartedException() 
        : base("RIDE_003", "El viaje ya partió", 409) { }
}

public class CannotReserveOwnRideException : BusinessException
{
    public CannotReserveOwnRideException() 
        : base("RIDE_004", "No podés reservar tu propio viaje", 409) { }
}

// ========== RESERVATION EXCEPTIONS ==========

public class ReservationNotFoundException : BusinessException
{
    public ReservationNotFoundException(Guid reservationId) 
        : base("RESV_001", $"Reserva no encontrada: {reservationId}", 404) { }
}

public class ReservationAlreadyExistsException : BusinessException
{
    public ReservationAlreadyExistsException() 
        : base("RESV_002", "Ya tenés una reserva para este viaje", 409) { }
}

public class ReservationNotCancellableException : BusinessException
{
    public ReservationNotCancellableException() 
        : base("RESV_003", "La reserva no puede cancelarse en este estado", 409) { }
}

// ========== PAYMENT EXCEPTIONS ==========

public class PaymentFailedException : BusinessException
{
    public PaymentFailedException(string reason) 
        : base("PAY_001", $"Error en el pago: {reason}", 402) { }
}

public class PaymentNotFoundException : BusinessException
{
    public PaymentNotFoundException(Guid paymentId) 
        : base("PAY_002", $"Pago no encontrado: {paymentId}", 404) { }
}

// ========== VERIFICATION EXCEPTIONS ==========

public class VerificationPendingException : BusinessException
{
    public VerificationPendingException() 
        : base("VERIF_001", "Tu verificación está pendiente de revisión", 409) { }
}

public class NotVerifiedException : BusinessException
{
    public NotVerifiedException() 
        : base("VERIF_002", "Necesitás verificar tu identidad para realizar esta acción", 403) { }
}
