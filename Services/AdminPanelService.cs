using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SubiteAPI.Data;
using SubiteAPI.DTOs;
using SubiteAPI.Exceptions;
using SubiteAPI.Models;

namespace SubiteAPI.Services;

public interface IAdminPanelService
{
    Task<AdminDashboardKpisDto> GetKpisAsync(DateTime? desde, DateTime? hasta);
    Task<IReadOnlyList<AdminDashboardSeriePointDto>> GetSeriesAsync(int days = 7);
    Task<AdminPagedResultDto<AdminUsuarioDto>> GetUsuariosAsync(string? query, int page, int pageSize);
    Task BloquearUsuarioAsync(Guid userId, bool bloquear);
    Task<AdminPagedResultDto<AdminViajeDto>> GetViajesAsync(string? query, string? estado, int page, int pageSize);
    Task CancelarViajeAsync(Guid rideId, string? reason);
    Task<AdminPagedResultDto<AdminReservaDto>> GetReservasAsync(string? query, string? estado, int page, int pageSize);
    Task<AdminGananciasDto> GetGananciasAsync(int days = 30);
    Task<AdminPagedResultDto<AdminLogDto>> GetLogsAsync(string? query, int page, int pageSize);
}

public class AdminPanelService : IAdminPanelService
{
    private readonly AppDbContext _db;
    private readonly UserManager<User> _userManager;

    public AdminPanelService(AppDbContext db, UserManager<User> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<AdminDashboardKpisDto> GetKpisAsync(DateTime? desde, DateTime? hasta)
    {
        var rides = _db.Rides.AsNoTracking();
        var reservations = _db.Reservations.AsNoTracking();
        var payments = _db.Payments.AsNoTracking();

        if (desde.HasValue)
        {
            rides = rides.Where(r => r.CreatedAt >= desde.Value);
            reservations = reservations.Where(r => r.CreatedAt >= desde.Value);
            payments = payments.Where(p => p.CreatedAt >= desde.Value);
        }

        if (hasta.HasValue)
        {
            rides = rides.Where(r => r.CreatedAt <= hasta.Value);
            reservations = reservations.Where(r => r.CreatedAt <= hasta.Value);
            payments = payments.Where(p => p.CreatedAt <= hasta.Value);
        }

        return new AdminDashboardKpisDto
        {
            Usuarios = await _db.Users.AsNoTracking().CountAsync().ConfigureAwait(false),
            ViajesPublicados = await rides.CountAsync().ConfigureAwait(false),
            Reservas = await reservations.CountAsync().ConfigureAwait(false),
            PagosOk = await payments.CountAsync(p => p.Status == PaymentStatus.Approved).ConfigureAwait(false),
            PagosFail = await payments.CountAsync(p =>
                    p.Status == PaymentStatus.Rejected || p.Status == PaymentStatus.Cancelled)
                .ConfigureAwait(false),
            GmvAproximado = await payments
                .Where(p => p.Status == PaymentStatus.Approved)
                .SumAsync(p => (decimal?)p.Amount)
                .ConfigureAwait(false) ?? 0
        };
    }

    public async Task<IReadOnlyList<AdminDashboardSeriePointDto>> GetSeriesAsync(int days = 7)
    {
        days = Math.Clamp(days, 1, 30);
        var from = DateTime.UtcNow.Date.AddDays(1 - days);
        var counts = await _db.Reservations.AsNoTracking()
            .Where(r => r.CreatedAt >= from)
            .GroupBy(r => r.CreatedAt.Date)
            .Select(g => new { Day = g.Key, Count = g.Count() })
            .ToListAsync()
            .ConfigureAwait(false);

        var lookup = counts.ToDictionary(x => x.Day, x => x.Count);
        return Enumerable.Range(0, days)
            .Select(offset =>
            {
                var day = from.AddDays(offset);
                return new AdminDashboardSeriePointDto
                {
                    Fecha = day.ToString("dd/MM"),
                    Reservas = lookup.TryGetValue(day, out var count) ? count : 0
                };
            })
            .ToList();
    }

    public async Task<AdminPagedResultDto<AdminUsuarioDto>> GetUsuariosAsync(
        string? query, int page, int pageSize)
    {
        (page, pageSize) = NormalizePaging(page, pageSize);
        var q = query?.Trim() ?? string.Empty;

        var users = _db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q))
        {
            users = users.Where(u =>
                (u.FullName != null && u.FullName.Contains(q)) ||
                (u.Email != null && u.Email.Contains(q)));
        }

        var total = await users.CountAsync().ConfigureAwait(false);
        var pageItems = await users
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync()
            .ConfigureAwait(false);

        var items = new List<AdminUsuarioDto>(pageItems.Count);
        foreach (var user in pageItems)
        {
            var roles = await _userManager.GetRolesAsync(user).ConfigureAwait(false);
            items.Add(new AdminUsuarioDto
            {
                Id = user.Id,
                Nombre = user.FullName,
                Email = user.Email ?? string.Empty,
                Rol = ResolveRol(user, roles),
                Estado = IsBlocked(user) ? "Bloqueado" : "Activo",
                Creado = user.CreatedAt
            });
        }

        return Page(items, total, page, pageSize);
    }

    public async Task BloquearUsuarioAsync(Guid userId, bool bloquear)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString()).ConfigureAwait(false)
            ?? throw new BusinessException("ADM_USER_001", "Usuario no encontrado.", 404);

        if (await _userManager.IsInRoleAsync(user, AppRoles.Admin).ConfigureAwait(false))
        {
            throw new BusinessException("ADM_USER_002", "No se puede bloquear un administrador.", 409);
        }

        user.LockoutEnabled = true;
        user.LockoutEnd = bloquear
            ? DateTimeOffset.UtcNow.AddYears(50)
            : null;

        var result = await _userManager.UpdateAsync(user).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new BusinessException("ADM_USER_003", "No se pudo actualizar el usuario.", 500);
        }
    }

    public async Task<AdminPagedResultDto<AdminViajeDto>> GetViajesAsync(
        string? query, string? estado, int page, int pageSize)
    {
        (page, pageSize) = NormalizePaging(page, pageSize);
        var rides = _db.Rides.AsNoTracking().Include(r => r.Driver).AsQueryable();

        if (TryParseRideStatus(estado, out var status))
        {
            rides = rides.Where(r => r.Status == status);
        }

        var q = query?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(q))
        {
            rides = rides.Where(r =>
                r.OriginCity.Contains(q) ||
                r.DestinationCity.Contains(q) ||
                r.Driver.FullName.Contains(q) ||
                (r.Driver.Email != null && r.Driver.Email.Contains(q)));
        }

        var total = await rides.CountAsync().ConfigureAwait(false);
        var items = await rides
            .OrderByDescending(r => r.DepartureDateTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new AdminViajeDto
            {
                Id = r.Id,
                Origen = r.OriginCity,
                Destino = r.DestinationCity,
                Salida = r.DepartureDateTime,
                Estado = r.Status.ToString(),
                ConductorNombre = r.Driver.FullName,
                ConductorEmail = r.Driver.Email ?? string.Empty,
                AsientosTotales = r.TotalSeats,
                AsientosDisponibles = r.AvailableSeats,
                PrecioPorAsiento = r.PricePerSeat,
                ReservasActivas = r.Reservations.Count(x =>
                    x.Status == ReservationStatus.Pending || x.Status == ReservationStatus.Confirmed)
            })
            .ToListAsync()
            .ConfigureAwait(false);

        return Page(items, total, page, pageSize);
    }

    public async Task CancelarViajeAsync(Guid rideId, string? reason)
    {
        var ride = await _db.Rides.FirstOrDefaultAsync(r => r.Id == rideId).ConfigureAwait(false)
            ?? throw new RideNotFoundException(rideId);

        if (ride.Status is RideStatus.Completed or RideStatus.Cancelled)
        {
            throw new BusinessException("ADM_RIDE_001", "El viaje ya está finalizado o cancelado.", 409);
        }

        ride.Status = RideStatus.Cancelled;

        var reservations = await _db.Reservations
            .Where(r => r.RideId == rideId &&
                        (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Confirmed))
            .ToListAsync()
            .ConfigureAwait(false);

        var message = string.IsNullOrWhiteSpace(reason)
            ? "Cancelado por administración."
            : reason.Trim();

        foreach (var reservation in reservations)
        {
            reservation.Status = ReservationStatus.Cancelled;
            reservation.CancelledAt = DateTime.UtcNow;
            reservation.CancellationReason = message;
        }

        await _db.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task<AdminPagedResultDto<AdminReservaDto>> GetReservasAsync(
        string? query, string? estado, int page, int pageSize)
    {
        (page, pageSize) = NormalizePaging(page, pageSize);
        var reservations = _db.Reservations.AsNoTracking()
            .Include(r => r.Passenger)
            .Include(r => r.Ride)
            .Include(r => r.Payment)
            .AsQueryable();

        if (TryParseReservationStatus(estado, out var status))
        {
            reservations = reservations.Where(r => r.Status == status);
        }

        var q = query?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(q))
        {
            reservations = reservations.Where(r =>
                r.Passenger.FullName.Contains(q) ||
                (r.Passenger.Email != null && r.Passenger.Email.Contains(q)) ||
                r.Ride.OriginCity.Contains(q) ||
                r.Ride.DestinationCity.Contains(q));
        }

        var total = await reservations.CountAsync().ConfigureAwait(false);
        var items = await reservations
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new AdminReservaDto
            {
                Id = r.Id,
                RideId = r.RideId,
                Ruta = r.Ride.OriginCity + " → " + r.Ride.DestinationCity,
                PasajeroNombre = r.Passenger.FullName,
                PasajeroEmail = r.Passenger.Email ?? string.Empty,
                Asientos = r.SeatsReserved,
                Monto = r.Payment != null ? r.Payment.Amount : r.TotalPrice,
                BaseViaje = r.TotalPrice,
                Ganancia = r.Payment != null && r.Payment.Status == PaymentStatus.Approved
                    ? Math.Max(0, r.Payment.Amount - r.TotalPrice)
                    : 0,
                Estado = r.Status.ToString(),
                EstadoPago = r.Payment != null ? r.Payment.Status.ToString() : "SinPago",
                Creada = r.CreatedAt
            })
            .ToListAsync()
            .ConfigureAwait(false);

        return Page(items, total, page, pageSize);
    }

    public async Task<AdminGananciasDto> GetGananciasAsync(int days = 30)
    {
        days = Math.Clamp(days, 1, 180);
        var from = DateTime.UtcNow.Date.AddDays(1 - days);

        var approved = await _db.Payments.AsNoTracking()
            .Include(p => p.Reservation)
            .ThenInclude(r => r.Ride)
            .Where(p => p.Status == PaymentStatus.Approved && p.CreatedAt >= from)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync()
            .ConfigureAwait(false);

        var pagos = approved.Select(p =>
        {
            var baseViaje = p.Reservation?.TotalPrice ?? 0;
            var ganancia = Math.Max(0, p.Amount - baseViaje);
            var ride = p.Reservation?.Ride;
            return new AdminGananciaPagoDto
            {
                Id = p.Id,
                Ruta = ride == null ? "—" : $"{ride.OriginCity} → {ride.DestinationCity}",
                Fecha = p.CreatedAt,
                MontoPasajero = p.Amount,
                BaseViaje = baseViaje,
                Ganancia = ganancia,
                Estado = p.Status.ToString()
            };
        }).ToList();

        var totalGanancia = pagos.Sum(p => p.Ganancia);
        var totalGmv = pagos.Sum(p => p.MontoPasajero);

        var lookup = pagos
            .GroupBy(p => p.Fecha.Date)
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    Ganancia = g.Sum(x => x.Ganancia),
                    Gmv = g.Sum(x => x.MontoPasajero),
                    Pagos = g.Count()
                });

        var series = new List<AdminGananciaSeriePointDto>();
        for (var i = 0; i < days; i++)
        {
            var day = from.AddDays(i);
            lookup.TryGetValue(day, out var point);
            series.Add(new AdminGananciaSeriePointDto
            {
                Fecha = day.ToString("dd/MM"),
                Ganancia = point?.Ganancia ?? 0,
                Gmv = point?.Gmv ?? 0,
                Pagos = point?.Pagos ?? 0
            });
        }

        return new AdminGananciasDto
        {
            Days = days,
            TotalGanancia = totalGanancia,
            TotalGmv = totalGmv,
            PagosOk = pagos.Count,
            GananciaPromedio = pagos.Count == 0 ? 0 : Math.Round(totalGanancia / pagos.Count, 0),
            Series = series,
            Pagos = pagos.Take(40).ToList()
        };
    }

    public async Task<AdminPagedResultDto<AdminLogDto>> GetLogsAsync(
        string? query, int page, int pageSize)
    {
        (page, pageSize) = NormalizePaging(page, pageSize);

        var notifications = await _db.AppNotifications.AsNoTracking()
            .OrderByDescending(n => n.CreatedAt)
            .Take(200)
            .Select(n => new AdminLogDto
            {
                Id = n.Id,
                Fecha = n.CreatedAt,
                Tipo = n.Type,
                Titulo = n.Title,
                Detalle = n.Body
            })
            .ToListAsync()
            .ConfigureAwait(false);

        var payments = await _db.Payments.AsNoTracking()
            .OrderByDescending(p => p.CreatedAt)
            .Take(100)
            .Select(p => new AdminLogDto
            {
                Id = p.Id,
                Fecha = p.CreatedAt,
                Tipo = "payment",
                Titulo = "Pago " + p.Status.ToString(),
                Detalle = "Reserva " + p.ReservationId + " · $" + p.Amount
            })
            .ToListAsync()
            .ConfigureAwait(false);

        var cancelledRides = await _db.Rides.AsNoTracking()
            .Where(r => r.Status == RideStatus.Cancelled)
            .OrderByDescending(r => r.CreatedAt)
            .Take(50)
            .Select(r => new AdminLogDto
            {
                Id = r.Id,
                Fecha = r.CreatedAt,
                Tipo = "ride",
                Titulo = "Viaje cancelado",
                Detalle = r.OriginCity + " → " + r.DestinationCity
            })
            .ToListAsync()
            .ConfigureAwait(false);

        var merged = notifications
            .Concat(payments)
            .Concat(cancelledRides)
            .OrderByDescending(x => x.Fecha)
            .AsEnumerable();

        var q = query?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(q))
        {
            merged = merged.Where(x =>
                x.Titulo.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                x.Detalle.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                x.Tipo.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        var list = merged.ToList();
        var items = list.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Page(items, list.Count, page, pageSize);
    }

    private static AdminPagedResultDto<T> Page<T>(
        IReadOnlyList<T> items, int total, int page, int pageSize) =>
        new()
        {
            Items = items,
            Total = total,
            Page = page,
            PageSize = pageSize
        };

    private static (int page, int pageSize) NormalizePaging(int page, int pageSize)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;
        return (page, pageSize);
    }

    private static bool IsBlocked(User user) =>
        user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow;

    private static string ResolveRol(User user, IList<string> roles)
    {
        if (roles.Contains(AppRoles.Admin)) return "Admin";
        return user.IsDriver ? "Conductor" : "Pasajero";
    }

    private static bool TryParseRideStatus(string? value, out RideStatus status)
    {
        status = default;
        return !string.IsNullOrWhiteSpace(value) &&
               Enum.TryParse(value, ignoreCase: true, out status);
    }

    private static bool TryParseReservationStatus(string? value, out ReservationStatus status)
    {
        status = default;
        return !string.IsNullOrWhiteSpace(value) &&
               Enum.TryParse(value, ignoreCase: true, out status);
    }
}
