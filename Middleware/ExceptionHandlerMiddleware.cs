using System.Text.Json;
using SubiteAPI.Exceptions;

namespace SubiteAPI.Middleware;

public class ExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlerMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionHandlerMiddleware(
        RequestDelegate next, 
        ILogger<ExceptionHandlerMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex).ConfigureAwait(false);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var response = context.Response;
        response.ContentType = "application/json";

        var (statusCode, errorResponse) = exception switch
        {
            BusinessException businessEx => HandleBusinessException(businessEx),
            InfrastructureException infraEx => HandleInfrastructureException(infraEx),
            _ => HandleUnknownException(exception)
        };

        response.StatusCode = statusCode;

        var json = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await response.WriteAsync(json).ConfigureAwait(false);
    }

    private (int, object) HandleBusinessException(BusinessException ex)
    {
        _logger.LogWarning(
            "Business exception: {Code} - {Message}", 
            ex.Code, 
            ex.Message);

        return (ex.StatusCode, new ErrorResponse
        {
            Success = false,
            Error = ex.Code,
            Message = ex.Message
        });
    }

    private (int, object) HandleInfrastructureException(InfrastructureException ex)
    {
        _logger.LogError(
            ex,
            "Infrastructure exception: {Code} - {Message}",
            ex.Code,
            ex.Message);

        return (500, new ErrorResponse
        {
            Success = false,
            Error = "SERVER_ERROR",
            Message = "Error interno del servidor. Intentá de nuevo más tarde."
        });
    }

    private (int, object) HandleUnknownException(Exception ex)
    {
        _logger.LogError(
            ex,
            "Unhandled exception: {Message}",
            ex.Message);

        var response = new ErrorResponse
        {
            Success = false,
            Error = "SERVER_ERROR",
            Message = "Error interno del servidor"
        };

        // En desarrollo, incluir detalles del error
        if (_env.IsDevelopment())
        {
            response.Details = new ErrorDetails
            {
                Type = ex.GetType().Name,
                Message = ex.Message,
                StackTrace = ex.StackTrace
            };
        }

        return (500, response);
    }
}

public class ErrorResponse
{
    public bool Success { get; set; }
    public string Error { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public ErrorDetails? Details { get; set; }
}

public class ErrorDetails
{
    public string? Type { get; set; }
    public string? Message { get; set; }
    public string? StackTrace { get; set; }
}

// Extension method para registrar el middleware
public static class ExceptionHandlerMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ExceptionHandlerMiddleware>();
    }
}
