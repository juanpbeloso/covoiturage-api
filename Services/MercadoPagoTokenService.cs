using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SubiteAPI.Data;
using SubiteAPI.DTOs;
using SubiteAPI.Exceptions;
using SubiteAPI.Models;
using SubiteAPI.Options;

namespace SubiteAPI.Services;

public interface IMercadoPagoTokenService
{
    Task<bool> IsConnectedAsync(Guid conductorId);
    Task<MercadoPagoConnectionStatusDto> GetStatusAsync(Guid conductorId);
    string BuildAuthorizationUrl(Guid conductorId);
    Task CompleteOAuthAsync(string code, string state);
    /// <summary>Devuelve un access_token válido del conductor (renueva si está por vencer).</summary>
    Task<string> GetValidAccessTokenAsync(Guid conductorId);
}

public class MercadoPagoTokenService : IMercadoPagoTokenService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly AppDbContext _db;
    private readonly MercadoPagoOptions _options;
    private readonly AppOptions _appOptions;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<MercadoPagoTokenService> _logger;

    public MercadoPagoTokenService(
        AppDbContext db,
        IOptions<MercadoPagoOptions> options,
        IOptions<AppOptions> appOptions,
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<MercadoPagoTokenService> logger)
    {
        _db = db;
        _options = options.Value;
        _appOptions = appOptions.Value;
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
    }

    public Task<bool> IsConnectedAsync(Guid conductorId) =>
        _db.ConductorMercadoPagos.AsNoTracking().AnyAsync(c => c.ConductorId == conductorId);

    public async Task<MercadoPagoConnectionStatusDto> GetStatusAsync(Guid conductorId)
    {
        var row = await _db.ConductorMercadoPagos.AsNoTracking()
            .FirstOrDefaultAsync(c => c.ConductorId == conductorId)
            .ConfigureAwait(false);

        if (row == null)
        {
            return new MercadoPagoConnectionStatusDto { Connected = false };
        }

        return new MercadoPagoConnectionStatusDto
        {
            Connected = true,
            MpUserId = row.MpUserId,
            ConnectedAt = row.ConectadoEn
        };
    }

    public string BuildAuthorizationUrl(Guid conductorId)
    {
        EnsureOAuthConfigured();

        var redirectUri = ResolveRedirectUri();
        var state = CreateSignedState(conductorId);
        var query = new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["response_type"] = "code",
            ["platform_id"] = "mp",
            ["state"] = state,
            ["redirect_uri"] = redirectUri
        };

        var qs = string.Join("&", query.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

        return $"https://auth.mercadopago.com.ar/authorization?{qs}";
    }

    public async Task CompleteOAuthAsync(string code, string state)
    {
        EnsureOAuthConfigured();

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new BusinessException("MP_OAUTH_001", "Falta el código de autorización de MercadoPago.");
        }

        var conductorId = ParseSignedState(state);
        var token = await ExchangeCodeAsync(code).ConfigureAwait(false);
        await UpsertConnectionAsync(conductorId, token).ConfigureAwait(false);
    }

    public async Task<string> GetValidAccessTokenAsync(Guid conductorId)
    {
        var row = await _db.ConductorMercadoPagos
            .FirstOrDefaultAsync(c => c.ConductorId == conductorId)
            .ConfigureAwait(false)
            ?? throw new BusinessException(
                "MP_SELLER_001",
                "El conductor no tiene una cuenta de MercadoPago conectada.",
                409);

        var skew = TimeSpan.FromMinutes(Math.Max(1, _options.TokenRefreshSkewMinutes));
        if (row.TokenExpiraEn <= DateTime.UtcNow.Add(skew))
        {
            _logger.LogInformation("Renovando token MP del conductor {ConductorId}", conductorId);
            var refreshed = await RefreshTokenAsync(row.RefreshToken).ConfigureAwait(false);
            ApplyToken(row, refreshed, keepConnectedAt: true);
            await _db.SaveChangesAsync().ConfigureAwait(false);
        }

        return row.AccessToken;
    }

    private async Task UpsertConnectionAsync(Guid conductorId, MercadoPagoOAuthTokenResponse token)
    {
        var row = await _db.ConductorMercadoPagos
            .FirstOrDefaultAsync(c => c.ConductorId == conductorId)
            .ConfigureAwait(false);

        if (row == null)
        {
            row = new ConductorMercadoPago
            {
                ConductorId = conductorId,
                ConectadoEn = DateTime.UtcNow
            };
            _db.ConductorMercadoPagos.Add(row);
        }

        ApplyToken(row, token, keepConnectedAt: row.AccessToken.Length > 0);
        if (string.IsNullOrEmpty(row.AccessToken) || row.ConectadoEn == default)
        {
            row.ConectadoEn = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync().ConfigureAwait(false);
        _logger.LogInformation(
            "Cuenta MP conectada para conductor {ConductorId} (mpUserId={MpUserId})",
            conductorId,
            row.MpUserId);
    }

    private static void ApplyToken(
        ConductorMercadoPago row,
        MercadoPagoOAuthTokenResponse token,
        bool keepConnectedAt)
    {
        if (string.IsNullOrWhiteSpace(token.AccessToken))
        {
            throw new BusinessException("MP_OAUTH_002", "MercadoPago no devolvió access_token.");
        }

        row.AccessToken = token.AccessToken;
        if (!string.IsNullOrWhiteSpace(token.RefreshToken))
        {
            row.RefreshToken = token.RefreshToken;
        }

        row.MpUserId = token.UserId.ToString();
        var expiresIn = token.ExpiresIn > 0 ? token.ExpiresIn : 15552000; // ~180 días default MP
        row.TokenExpiraEn = DateTime.UtcNow.AddSeconds(expiresIn);
        row.UpdatedAt = DateTime.UtcNow;
        if (!keepConnectedAt)
        {
            row.ConectadoEn = DateTime.UtcNow;
        }
    }

    private async Task<MercadoPagoOAuthTokenResponse> ExchangeCodeAsync(string code)
    {
        var payload = new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = ResolveRedirectUri()
        };

        return await PostOAuthTokenAsync(payload).ConfigureAwait(false);
    }

    private async Task<MercadoPagoOAuthTokenResponse> RefreshTokenAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new BusinessException(
                "MP_SELLER_002",
                "El token de MercadoPago del conductor expiró. Pedile que reconecte su cuenta.",
                409);
        }

        var payload = new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken
        };

        return await PostOAuthTokenAsync(payload).ConfigureAwait(false);
    }

    private async Task<MercadoPagoOAuthTokenResponse> PostOAuthTokenAsync(Dictionary<string, string> payload)
    {
        var client = _httpClientFactory.CreateClient("MercadoPagoOAuth");
        using var content = new FormUrlEncodedContent(payload);
        using var response = await client
            .PostAsync("https://api.mercadopago.com/oauth/token", content)
            .ConfigureAwait(false);

        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("OAuth MP error {Status}: {Body}", (int)response.StatusCode, body);
            throw new BusinessException(
                "MP_OAUTH_003",
                "No se pudo completar la autorización con MercadoPago. Intentá de nuevo.");
        }

        var token = JsonSerializer.Deserialize<MercadoPagoOAuthTokenResponse>(body, JsonOptions)
            ?? throw new BusinessException("MP_OAUTH_002", "Respuesta OAuth inválida de MercadoPago.");

        return token;
    }

    private string ResolveRedirectUri()
    {
        if (!string.IsNullOrWhiteSpace(_options.RedirectUri))
        {
            return _options.RedirectUri.Trim();
        }

        var backend = _appOptions.BackendUrl?.TrimEnd('/') ?? "";
        if (string.IsNullOrWhiteSpace(backend))
        {
            throw new BusinessException(
                "MP_OAUTH_004",
                "Configurá MercadoPago:RedirectUri o App:BackendUrl para el callback OAuth.");
        }

        return $"{backend}/api/conductores/mercadopago/callback";
    }

    private void EnsureOAuthConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId) ||
            string.IsNullOrWhiteSpace(_options.ClientSecret))
        {
            throw new BusinessException(
                "MP_OAUTH_005",
                "MercadoPago OAuth no está configurado (ClientId / ClientSecret).");
        }
    }

    private string CreateSignedState(Guid conductorId)
    {
        var expires = DateTimeOffset.UtcNow.AddMinutes(15).ToUnixTimeSeconds();
        var payload = $"{conductorId:N}|{expires}";
        var sig = Sign(payload);
        return Base64UrlEncode(Encoding.UTF8.GetBytes($"{payload}|{sig}"));
    }

    private Guid ParseSignedState(string state)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            throw new BusinessException("MP_OAUTH_006", "State OAuth inválido o ausente.");
        }

        string decoded;
        try
        {
            decoded = Encoding.UTF8.GetString(Base64UrlDecode(state));
        }
        catch
        {
            throw new BusinessException("MP_OAUTH_006", "State OAuth inválido.");
        }

        var parts = decoded.Split('|');
        if (parts.Length != 3 ||
            !Guid.TryParseExact(parts[0], "N", out var conductorId) ||
            !long.TryParse(parts[1], out var expiresUnix))
        {
            throw new BusinessException("MP_OAUTH_006", "State OAuth inválido.");
        }

        var payload = $"{parts[0]}|{parts[1]}";
        var expected = Sign(payload);
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected),
                Encoding.UTF8.GetBytes(parts[2])))
        {
            throw new BusinessException("MP_OAUTH_006", "State OAuth inválido.");
        }

        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiresUnix)
        {
            throw new BusinessException("MP_OAUTH_007", "La autorización expiró. Volvé a conectar MercadoPago.");
        }

        return conductorId;
    }

    private string Sign(string payload)
    {
        var key = Encoding.UTF8.GetBytes(_config["Jwt:Key"] ?? "subite-mp-oauth-fallback-key");
        using var hmac = new HMACSHA256(key);
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
    }

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string input)
    {
        var s = input.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }
        return Convert.FromBase64String(s);
    }
}
