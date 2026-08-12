using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SubiteAPI.Data;
using SubiteAPI.DTOs;
using SubiteAPI.Exceptions;
using SubiteAPI.Models;
using SubiteAPI.Options;

namespace SubiteAPI.Services;

public interface IDiditService
{
    Task<DiditSessionResultDto> CreateSessionAsync(Guid userId);
    Task<DiditStatusDto> GetStatusAsync(Guid userId);
    Task ProcessWebhookAsync(string rawBody, string? signatureV2, string? signature, string? timestampHeader);
}

public class DiditService : IDiditService
{
    private static readonly string[] OpenStatuses =
    {
        "Not Started", "In Progress", "Resubmitted", "Awaiting User"
    };

    private readonly AppDbContext _db;
    private readonly DiditOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DiditService> _logger;

    public DiditService(
        AppDbContext db,
        IOptions<DiditOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<DiditService> logger)
    {
        _db = db;
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<DiditSessionResultDto> CreateSessionAsync(Guid userId)
    {
        EnsureConfigured();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId).ConfigureAwait(false)
            ?? throw new BusinessException("AUTH_001", "Usuario no encontrado.", 404);

        if (user.IsVerified)
        {
            var latestApproved = await _db.DiditSessions
                .AsNoTracking()
                .Where(s => s.UserId == userId && s.Status == "Approved")
                .OrderByDescending(s => s.UpdatedAt)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);

            return new DiditSessionResultDto
            {
                SessionId = latestApproved?.SessionId ?? string.Empty,
                Url = string.Empty,
                Status = "Approved",
                IsVerified = true
            };
        }

        var open = await _db.DiditSessions
            .Where(s => s.UserId == userId && OpenStatuses.Contains(s.Status) && s.VerificationUrl != null)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (open != null && !string.IsNullOrWhiteSpace(open.VerificationUrl))
        {
            return new DiditSessionResultDto
            {
                SessionId = open.SessionId,
                Url = open.VerificationUrl,
                Status = open.Status,
                IsVerified = false
            };
        }

        var client = _httpClientFactory.CreateClient("Didit");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl.TrimEnd('/')}/session/");
        request.Headers.TryAddWithoutValidation("x-api-key", _options.ApiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(new
            {
                workflow_id = _options.WorkflowId,
                vendor_data = userId.ToString(),
                callback = _options.CallbackUrl,
                metadata = new { app = "subite", full_name = user.FullName }
            }),
            Encoding.UTF8,
            "application/json");

        using var response = await client.SendAsync(request).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Didit create session failed: {Status} {Body}", (int)response.StatusCode, body);
            throw new BusinessException(
                "DIDIT_002",
                "No se pudo iniciar la verificación de identidad. Revisá WorkflowId/API key en Didit.",
                502);
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var sessionId = root.GetProperty("session_id").GetString()
            ?? throw new BusinessException("DIDIT_002", "Didit no devolvió session_id.", 502);
        var url = root.TryGetProperty("url", out var urlEl)
            ? urlEl.GetString()
            : root.TryGetProperty("verification_url", out var vUrl) ? vUrl.GetString() : null;
        var status = root.TryGetProperty("status", out var st) ? st.GetString() ?? "Not Started" : "Not Started";

        if (string.IsNullOrWhiteSpace(url))
        {
            throw new BusinessException("DIDIT_002", "Didit no devolvió URL de verificación.", 502);
        }

        var entity = new DiditSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SessionId = sessionId,
            Status = status,
            VerificationUrl = url,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.DiditSessions.Add(entity);
        await _db.SaveChangesAsync().ConfigureAwait(false);

        return new DiditSessionResultDto
        {
            SessionId = sessionId,
            Url = url,
            Status = status,
            IsVerified = false
        };
    }

    public async Task<DiditStatusDto> GetStatusAsync(Guid userId)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId).ConfigureAwait(false)
            ?? throw new BusinessException("AUTH_001", "Usuario no encontrado.", 404);

        var latest = await _db.DiditSessions
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.UpdatedAt)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        // Si hay sesión abierta, intentar sincronizar con Didit (útil sin webhook en local).
        if (latest != null &&
            !user.IsVerified &&
            OpenStatuses.Contains(latest.Status) &&
            _options.IsConfigured)
        {
            await TrySyncSessionFromDiditAsync(latest.SessionId, userId).ConfigureAwait(false);
            user = await _db.Users.AsNoTracking().FirstAsync(u => u.Id == userId).ConfigureAwait(false);
            latest = await _db.DiditSessions
                .AsNoTracking()
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.UpdatedAt)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);
        }

        return new DiditStatusDto
        {
            IsVerified = user.IsVerified,
            SessionId = latest?.SessionId,
            Status = latest?.Status ?? (user.IsVerified ? "Approved" : null),
            Message = user.IsVerified
                ? "Identidad verificada."
                : latest == null
                    ? "Todavía no iniciaste la verificación."
                    : $"Estado: {latest.Status}"
        };
    }

    public async Task ProcessWebhookAsync(
        string rawBody,
        string? signatureV2,
        string? signature,
        string? timestampHeader)
    {
        if (!string.IsNullOrWhiteSpace(_options.WebhookSecret))
        {
            if (!long.TryParse(timestampHeader, out var ts) ||
                Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - ts) > 300)
            {
                throw new BusinessException("DIDIT_003", "Webhook timestamp inválido o expirado.", 401);
            }

            var valid =
                (!string.IsNullOrWhiteSpace(signatureV2) &&
                 VerifySignatureV2(rawBody, signatureV2, _options.WebhookSecret)) ||
                (!string.IsNullOrWhiteSpace(signature) &&
                 SecureEquals(HmacHex(rawBody, _options.WebhookSecret), signature));

            if (!valid)
            {
                throw new BusinessException("DIDIT_003", "Firma de webhook inválida.", 401);
            }
        }
        else
        {
            _logger.LogWarning("Didit WebhookSecret vacío: se acepta el webhook sin verificar firma (solo desarrollo).");
        }

        using var doc = JsonDocument.Parse(rawBody);
        var root = doc.RootElement;
        var webhookType = root.TryGetProperty("webhook_type", out var wt) ? wt.GetString() : null;
        if (!string.Equals(webhookType, "status.updated", StringComparison.Ordinal) &&
            !string.Equals(webhookType, "data.updated", StringComparison.Ordinal))
        {
            return;
        }

        var sessionId = root.TryGetProperty("session_id", out var sid) ? sid.GetString() : null;
        var status = root.TryGetProperty("status", out var st) ? st.GetString() : null;
        var eventId = root.TryGetProperty("event_id", out var eid) ? eid.GetString() : null;
        var vendorData = root.TryGetProperty("vendor_data", out var vd) ? vd.GetString() : null;

        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(status))
        {
            return;
        }

        await ApplySessionStatusAsync(sessionId, status, eventId, vendorData).ConfigureAwait(false);
    }

    private async Task TrySyncSessionFromDiditAsync(string sessionId, Guid userId)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("Didit");
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{_options.BaseUrl.TrimEnd('/')}/session/{sessionId}/decision/");
            request.Headers.TryAddWithoutValidation("x-api-key", _options.ApiKey);
            using var response = await client.SendAsync(request).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return;

            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var doc = JsonDocument.Parse(body);
            var status = doc.RootElement.TryGetProperty("status", out var st)
                ? st.GetString()
                : null;
            if (string.IsNullOrWhiteSpace(status)) return;

            await ApplySessionStatusAsync(sessionId, status, null, userId.ToString()).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "No se pudo sincronizar sesión Didit {SessionId}", sessionId);
        }
    }

    private async Task ApplySessionStatusAsync(
        string sessionId,
        string status,
        string? eventId,
        string? vendorData)
    {
        var session = await _db.DiditSessions
            .FirstOrDefaultAsync(s => s.SessionId == sessionId)
            .ConfigureAwait(false);

        if (session == null && Guid.TryParse(vendorData, out var userIdFromVendor))
        {
            session = new DiditSession
            {
                Id = Guid.NewGuid(),
                UserId = userIdFromVendor,
                SessionId = sessionId,
                Status = status,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.DiditSessions.Add(session);
        }

        if (session == null) return;

        if (!string.IsNullOrWhiteSpace(eventId) &&
            string.Equals(session.LastEventId, eventId, StringComparison.Ordinal))
        {
            return;
        }

        session.Status = status;
        session.UpdatedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(eventId))
        {
            session.LastEventId = eventId;
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == session.UserId).ConfigureAwait(false);
        if (user != null)
        {
            if (string.Equals(status, "Approved", StringComparison.Ordinal))
            {
                user.IsVerified = true;
            }
        }

        await _db.SaveChangesAsync().ConfigureAwait(false);
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new BusinessException("DIDIT_001", "Didit ApiKey no está configurada.", 503);
        }

        if (string.IsNullOrWhiteSpace(_options.WorkflowId))
        {
            throw new BusinessException(
                "DIDIT_001",
                "Falta Didit:WorkflowId. Creá un workflow KYC en business.didit.me y pegá el UUID en appsettings.",
                503);
        }
    }

    private static bool VerifySignatureV2(string rawBody, string providedHex, string secret)
    {
        try
        {
            var node = JsonNode.Parse(rawBody);
            if (node == null) return false;
            var canonical = CanonicalJson(node);
            return SecureEquals(HmacHex(canonical, secret), providedHex);
        }
        catch
        {
            return false;
        }
    }

    private static string CanonicalJson(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            var parts = obj
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv =>
                {
                    var value = kv.Value == null ? "null" : CanonicalJson(kv.Value);
                    return $"\"{Escape(kv.Key)}\":{value}";
                });
            return "{" + string.Join(",", parts) + "}";
        }

        if (node is JsonArray arr)
        {
            var parts = arr.Select(item => item == null ? "null" : CanonicalJson(item));
            return "[" + string.Join(",", parts) + "]";
        }

        if (node is JsonValue val)
        {
            if (val.TryGetValue<bool>(out var b)) return b ? "true" : "false";
            if (val.TryGetValue<long>(out var l)) return l.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (val.TryGetValue<double>(out var d))
            {
                // Prefer integer form when whole number to match Python sort_keys dumps for ints.
                if (Math.Abs(d % 1) < double.Epsilon)
                {
                    return ((long)d).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
                return d.ToString("G17", System.Globalization.CultureInfo.InvariantCulture);
            }
            if (val.TryGetValue<string>(out var s))
            {
                return JsonSerializer.Serialize(s); // keeps unicode unescaped by default in STJ? Actually escapes. Use custom.
            }
        }

        return node.ToJsonString(new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = false
        });
    }

    private static string Escape(string key) =>
        key.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

    private static string HmacHex(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool SecureEquals(string a, string b)
    {
        var ba = Encoding.UTF8.GetBytes(a.Trim().ToLowerInvariant());
        var bb = Encoding.UTF8.GetBytes(b.Trim().ToLowerInvariant());
        return ba.Length == bb.Length && CryptographicOperations.FixedTimeEquals(ba, bb);
    }
}
