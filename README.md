# Subite API

Backend .NET Core 8 para la aplicación de carpooling Subite.

## Stack

- .NET Core 8 Web API
- PostgreSQL + Entity Framework Core
- JWT Authentication
- MercadoPago SDK
- Firebase Admin (Push Notifications)
- Google Auth

## Requisitos

- .NET 8 SDK
- PostgreSQL 14+

## Setup

1. Configurar PostgreSQL y crear la base de datos:
```sql
CREATE DATABASE subite_db;
```

2. Actualizar `appsettings.json` con tu connection string:
```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=subite_db;Username=postgres;Password=tu_password"
}
```

3. Crear migración inicial:
```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

4. Ejecutar:
```bash
dotnet run
```

5. Abrir Swagger: http://localhost:5000/swagger

## Estructura

```
SubiteAPI/
├── Controllers/     # Endpoints API
├── Models/          # Entidades de BD
├── DTOs/            # Data Transfer Objects
├── Services/        # Lógica de negocio
├── Data/            # DbContext
├── Middleware/      # Custom middleware
└── Extensions/      # Extension methods
```

## Endpoints Principales

### Auth
- `POST /api/auth/register` - Registro con email
- `POST /api/auth/login` - Login con email
- `POST /api/auth/google` - Login con Google
- `POST /api/auth/apple` - Login con Apple
- `POST /api/auth/refresh` - Refrescar token

### Rides (pendiente)
- `GET /api/rides` - Listar viajes disponibles
- `POST /api/rides` - Crear viaje
- `GET /api/rides/{id}` - Detalle de viaje

### Reservations (pendiente)
- `POST /api/reservations` - Crear reserva
- `GET /api/reservations/my` - Mis reservas

### Payments (pendiente)
- `POST /api/payments/create-preference` - Crear preferencia MP
- `POST /api/payments/webhook` - Webhook de MercadoPago
