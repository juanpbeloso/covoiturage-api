# Deuda Técnica — SubiteAPI

**Fecha de análisis:** 8 de Junio 2026  
**Estándares de referencia:** Metafar .NET Guidelines (claude-code-skills)

---

## Resumen Ejecutivo

La API actual fue creada como MVP funcional. Para alinearla con los estándares de desarrollo de Metafar, se requieren los siguientes cambios priorizados en **BLOQUEANTES** y **SUGERENCIAS**.

| Categoría | Estado | Prioridad |
|-----------|--------|-----------|
| Clean Architecture | ❌ No implementado | BLOQUEANTE |
| CQRS (MediatR) | ❌ No implementado | BLOQUEANTE |
| ConfigureAwait(false) | ❌ Ausente | BLOQUEANTE |
| CancellationToken | ❌ No propagado | BLOQUEANTE |
| Nullable Reference Types | ❌ Deshabilitado | BLOQUEANTE |
| Excepciones tipadas | ❌ No existen | BLOQUEANTE |
| Logging estructurado | ❌ No implementado | SUGERENCIA |
| AsNoTracking() | ⚠️ Pendiente | SUGERENCIA |

---

## BLOQUEANTES — Resolver antes de producción

### B1. Sin separación de capas (Clean Architecture)

**Estado actual:**
```
SubiteAPI/
├── Controllers/
├── Models/
├── Services/
├── Data/
└── DTOs/
```

**Estado esperado:**
```
Subite/
├── Subite.API/              ← Capa de presentación
│   └── Controllers/
├── Subite.Application/      ← Casos de uso, Commands, Queries, Handlers
│   ├── Commands/
│   ├── Queries/
│   ├── Handlers/
│   ├── Interfaces/
│   └── DTOs/
├── Subite.Domain/           ← Entidades, Value Objects, Interfaces de repo
│   ├── Entities/
│   ├── ValueObjects/
│   ├── Interfaces/
│   └── Exceptions/
├── Subite.Infrastructure/   ← Implementaciones de BD, servicios externos
│   ├── Persistence/
│   ├── Services/
│   └── Repositories/
└── Subite.Tests/
```

**Reglas de dependencia:**
```
API           → Application, Infrastructure
Application   → Domain
Domain        → (ninguna dependencia)
Infrastructure → Application, Domain
```

**Impacto de no resolver:** Domain depende de Infrastructure (inversión de dependencias), código no testeable unitariamente, alto acoplamiento.

---

### B2. Sin CQRS (Commands/Queries/Handlers)

**Estado actual:**
```csharp
// AuthService.cs — 230 líneas, múltiples responsabilidades
public class AuthService : IAuthService
{
    public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto) { ... }
    public async Task<AuthResponseDto> LoginAsync(LoginDto dto) { ... }
    public async Task<AuthResponseDto> GoogleLoginAsync(string idToken) { ... }
}
```

**Estado esperado:**
```csharp
// Application/Commands/RegisterUserCommand.cs
public record RegisterUserCommand(string Email, string Password, string FullName) : IRequest<AuthResponse>;

// Application/Handlers/RegisterUserHandler.cs
public class RegisterUserHandler : IRequestHandler<RegisterUserCommand, AuthResponse>
{
    public async Task<AuthResponse> Handle(RegisterUserCommand request, CancellationToken ct)
    {
        // Lógica de registro
    }
}

// Controller — delgado
[HttpPost("register")]
public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
{
    var result = await _mediator.Send(new RegisterUserCommand(request.Email, request.Password, request.FullName), ct);
    return Ok(result);
}
```

**Paquete requerido:** `MediatR` + `MediatR.Extensions.Microsoft.DependencyInjection`

**Impacto de no resolver:** Servicios con múltiples responsabilidades (SRP), difícil de testear, difícil de extender.

---

### B3. ConfigureAwait(false) ausente

**Archivos afectados:**
- `Services/AuthService.cs` — 12 awaits sin ConfigureAwait
- `Services/JwtService.cs` — 0 awaits (OK)

**Ejemplo actual:**
```csharp
var user = await _userManager.FindByEmailAsync(dto.Email);
```

**Corrección:**
```csharp
var user = await _userManager.FindByEmailAsync(dto.Email).ConfigureAwait(false);
```

**Regla Metafar:** `ConfigureAwait(false)` en **todos** los awaits de servicios, repositorios y handlers.

**Impacto de no resolver:** Posibles deadlocks en contextos no-ASP.NET (tests, workers, etc.).

---

### B4. CancellationToken no propagado

**Estado actual:**
```csharp
// AuthController.cs
[HttpPost("register")]
public async Task<ActionResult<AuthResponseDto>> Register([FromBody] RegisterDto dto)
{
    var result = await _authService.RegisterAsync(dto);
    // ...
}
```

**Corrección:**
```csharp
[HttpPost("register")]
public async Task<ActionResult<AuthResponseDto>> Register(
    [FromBody] RegisterDto dto, 
    CancellationToken cancellationToken)
{
    var result = await _authService.RegisterAsync(dto, cancellationToken);
    // ...
}
```

**Archivos a modificar:**
- `Controllers/AuthController.cs` — todos los métodos
- `Services/IAuthService.cs` — todas las firmas
- `Services/AuthService.cs` — todas las implementaciones
- Propagarlo hasta `_userManager.CreateAsync(user, cancellationToken)`

**Impacto de no resolver:** Operaciones no cancelables, recursos colgados si el cliente cierra conexión.

---

### B5. Nullable Reference Types deshabilitado

**Estado actual en SubiteAPI.csproj:**
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <!-- Falta: <Nullable>enable</Nullable> -->
  </PropertyGroup>
</Project>
```

**Corrección:**
```xml
<PropertyGroup>
  <TargetFramework>net8.0</TargetFramework>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
</PropertyGroup>
```

**Impacto de no resolver:** NullReferenceException en runtime no detectadas en compilación, bugs silenciosos.

---

### B6. Sin excepciones tipadas

**Estado actual:**
```csharp
return new AuthResponseDto
{
    Success = false,
    Message = "Credenciales inválidas"  // Error de negocio como string
};
```

**Estado esperado:**
```csharp
// Domain/Exceptions/BusinessException.cs
public class BusinessException : Exception
{
    public string Code { get; }
    public BusinessException(string code, string message) : base(message)
    {
        Code = code;
    }
}

// Domain/Exceptions/InvalidCredentialsException.cs
public class InvalidCredentialsException : BusinessException
{
    public InvalidCredentialsException() 
        : base("AUTH_001", "Credenciales inválidas") { }
}

// Uso
throw new InvalidCredentialsException();
```

**Middleware de manejo:**
```csharp
// Middleware/ExceptionHandlerMiddleware.cs
app.UseExceptionHandler(handler =>
{
    handler.Run(async context =>
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        
        var (statusCode, response) = exception switch
        {
            BusinessException ex => (400, new { error = ex.Code, message = ex.Message }),
            _ => (500, new { error = "SERVER_ERROR", message = "Error interno" })
        };
        
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(response);
    });
});
```

**Impacto de no resolver:** Errores técnicos (stack traces, mensajes internos) llegan al cliente.

---

## SUGERENCIAS — Mejoran la calidad pero no son urgentes

### S1. Logging estructurado no implementado

**Estado actual:** Sin logging.

**Estado esperado:**
```csharp
public class RegisterUserHandler
{
    private readonly ILogger<RegisterUserHandler> _logger;

    public async Task<AuthResponse> Handle(RegisterUserCommand request, CancellationToken ct)
    {
        _logger.LogInformation("Registering user {Email}", request.Email);
        // ...
        _logger.LogInformation("User {UserId} registered successfully", user.Id);
    }
}
```

**Reglas:**
- Nunca interpolación: `_logger.LogError($"Error {id}")` ❌
- Usar structured logging: `_logger.LogError("Error {Id}", id)` ✅
- Nunca loguear datos sensibles (passwords, tokens)

---

### S2. AsNoTracking() pendiente

Cuando se implementen queries de lectura (listado de viajes, perfil de usuario, etc.):

```csharp
// ❌ Incorrecto
var rides = await _context.Rides.Where(...).ToListAsync(ct);

// ✅ Correcto
var rides = await _context.Rides
    .AsNoTracking()
    .Where(...)
    .ToListAsync(ct);
```

---

### S3. Métodos async sin sufijo Async

**Archivos afectados:**
- `Services/JwtService.cs` — `GenerateAccessToken` debería ser `GenerateAccessTokenAsync` si fuera async (actualmente es sync, OK)

---

## Plan de Refactor por Fases

### Fase 1 — Fixes críticos sin reestructurar (2-3 horas)
1. Agregar `<Nullable>enable</Nullable>` al .csproj
2. Agregar `ConfigureAwait(false)` a todos los awaits
3. Propagar `CancellationToken` en toda la cadena
4. Crear `BusinessException` y `InfrastructureException`
5. Agregar middleware de manejo de excepciones

### Fase 2 — Clean Architecture (1-2 días)
1. Crear proyectos separados (Domain, Application, Infrastructure, API)
2. Mover entidades a Domain
3. Mover servicios a Application
4. Mover DbContext a Infrastructure
5. Ajustar referencias entre proyectos

### Fase 3 — CQRS con MediatR (1-2 días)
1. Instalar MediatR
2. Crear Commands para operaciones de escritura
3. Crear Queries para operaciones de lectura
4. Crear Handlers
5. Adelgazar Controllers

### Fase 4 — Logging y Observabilidad (medio día)
1. Agregar ILogger a todos los handlers
2. Configurar Serilog o similar
3. Agregar correlation ID a los requests

---

## Checklist Pre-Producción

```markdown
[ ] ConfigureAwait(false) en todos los awaits
[ ] CancellationToken propagado hasta repositorios
[ ] <Nullable>enable</Nullable> en .csproj
[ ] Excepciones tipadas (BusinessException, InfrastructureException)
[ ] Middleware de manejo de excepciones
[ ] Logging estructurado (sin datos sensibles)
[ ] AsNoTracking() en queries de lectura
[ ] Sin magic strings (usar constantes)
[ ] Secrets en variables de entorno (no en appsettings.json)
[ ] Tests unitarios para handlers/servicios
```

---

## Referencias

- [Metafar .NET Guidelines](claude-code-skills-main/skills/code-implementation/SKILL.md)
- [Clean Architecture Validation](claude-code-skills-main/skills/architecture-validator/SKILL.md)
- [Refactor Guidelines](claude-code-skills-main/skills/refactor/SKILL.md)

---

*Documento generado el 8 de Junio 2026*
