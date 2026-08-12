namespace SubiteAPI.Exceptions;

/// <summary>
/// Excepción para errores de infraestructura (BD, servicios externos, etc.).
/// El mensaje interno NO se expone al cliente.
/// </summary>
public class InfrastructureException : Exception
{
    public string Code { get; }

    public InfrastructureException(string code, string internalMessage, Exception? innerException = null) 
        : base(internalMessage, innerException)
    {
        Code = code;
    }
}

public class DatabaseException : InfrastructureException
{
    public DatabaseException(string operation, Exception innerException) 
        : base("INFRA_DB", $"Error de base de datos en {operation}", innerException) { }
}

public class ExternalServiceException : InfrastructureException
{
    public string ServiceName { get; }

    public ExternalServiceException(string serviceName, string message, Exception? innerException = null) 
        : base("INFRA_EXT", $"Error en servicio externo {serviceName}: {message}", innerException)
    {
        ServiceName = serviceName;
    }
}

public class MercadoPagoException : ExternalServiceException
{
    public MercadoPagoException(string message, Exception? innerException = null) 
        : base("MercadoPago", message, innerException) { }
}

public class FirebaseException : ExternalServiceException
{
    public FirebaseException(string message, Exception? innerException = null) 
        : base("Firebase", message, innerException) { }
}

public class StorageException : ExternalServiceException
{
    public StorageException(string message, Exception? innerException = null) 
        : base("Storage", message, innerException) { }
}
