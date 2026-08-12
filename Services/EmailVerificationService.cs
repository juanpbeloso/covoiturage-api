using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SubiteAPI.Data;
using SubiteAPI.DTOs;
using SubiteAPI.Exceptions;
using SubiteAPI.Features.Email.Options;
using SubiteAPI.Features.Email.Services;
using SubiteAPI.Models;

namespace SubiteAPI.Services;

public interface IEmailVerificationService
{
    Task SendCodeAsync(User user);
    Task ResendCodeAsync(ResendVerificationDto dto);
    Task VerifyCodeAsync(VerifyEmailDto dto);
}

public class EmailVerificationService : IEmailVerificationService
{
    private const int CodeLength = 6;
    private const int ExpireMinutes = 15;
    private const int MaxAttempts = 5;

    private readonly UserManager<User> _userManager;
    private readonly AppDbContext _db;
    private readonly IEmailService _email;
    private readonly EnvialoSimpleOptions _emailOptions;
    private readonly ILogger<EmailVerificationService> _logger;

    public EmailVerificationService(
        UserManager<User> userManager,
        AppDbContext db,
        IEmailService email,
        IOptions<EnvialoSimpleOptions> emailOptions,
        ILogger<EmailVerificationService> logger)
    {
        _userManager = userManager;
        _db = db;
        _email = email;
        _emailOptions = emailOptions.Value;
        _logger = logger;
    }

    public async Task SendCodeAsync(User user)
    {
        if (user.EmailConfirmed)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var active = await _db.EmailVerificationCodes
            .Where(c => c.UserId == user.Id && c.UsedAt == null && c.ExpiresAt > now)
            .ToListAsync()
            .ConfigureAwait(false);

        foreach (var row in active)
        {
            row.UsedAt = now;
        }

        var code = GenerateNumericCode();
        _db.EmailVerificationCodes.Add(new EmailVerificationCode
        {
            UserId = user.Id,
            CodeHash = HashCode(code, user.Id),
            ExpiresAt = now.AddMinutes(ExpireMinutes),
            CreatedAt = now
        });
        await _db.SaveChangesAsync().ConfigureAwait(false);

        if (!_emailOptions.Enabled)
        {
            _logger.LogWarning(
                "EnvialoSimple deshabilitado. Código de verificación para {Email}: {Code}",
                user.Email,
                code);
            return;
        }

        try
        {
            await _email.SendTemplateAsync(
                user.Email!,
                "email-verification-code",
                new Dictionary<string, string>
                {
                    ["name"] = string.IsNullOrWhiteSpace(user.FullName) ? "hola" : user.FullName,
                    ["code"] = code,
                    ["minutes"] = ExpireMinutes.ToString()
                }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo enviar email de verificación a {Email}. Código: {Code}", user.Email, code);
            throw new BusinessException(
                "AUTH_EMAIL_SEND",
                "No pudimos enviar el email. Intentá de nuevo en unos minutos.");
        }
    }

    public async Task ResendCodeAsync(ResendVerificationDto dto)
    {
        var email = dto.Email.Trim();
        var user = await _userManager.FindByEmailAsync(email).ConfigureAwait(false);
        if (user == null)
        {
            _logger.LogInformation("Reenvío de verificación para email inexistente.");
            return;
        }

        if (user.EmailConfirmed)
        {
            return;
        }

        await SendCodeAsync(user).ConfigureAwait(false);
    }

    public async Task VerifyCodeAsync(VerifyEmailDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email.Trim()).ConfigureAwait(false)
            ?? throw new BusinessException("AUTH_VERIFY_INVALID", "Código inválido o vencido.");

        if (user.EmailConfirmed)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var entity = await _db.EmailVerificationCodes
            .Where(c => c.UserId == user.Id && c.UsedAt == null && c.ExpiresAt > now)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (entity == null)
        {
            throw new BusinessException("AUTH_VERIFY_INVALID", "Código inválido o vencido.");
        }

        if (entity.Attempts >= MaxAttempts)
        {
            entity.UsedAt = now;
            await _db.SaveChangesAsync().ConfigureAwait(false);
            throw new BusinessException("AUTH_VERIFY_LOCKED", "Demasiados intentos. Pedí un código nuevo.");
        }

        var hash = HashCode(dto.Code.Trim(), user.Id);
        var expected = Encoding.UTF8.GetBytes(entity.CodeHash);
        var actual = Encoding.UTF8.GetBytes(hash);
        if (expected.Length != actual.Length
            || !CryptographicOperations.FixedTimeEquals(expected, actual))
        {
            entity.Attempts += 1;
            await _db.SaveChangesAsync().ConfigureAwait(false);
            throw new BusinessException("AUTH_VERIFY_INVALID", "Código inválido o vencido.");
        }

        entity.UsedAt = now;
        user.EmailConfirmed = true;
        await _userManager.UpdateAsync(user).ConfigureAwait(false);
        await _db.SaveChangesAsync().ConfigureAwait(false);
    }

    private static string GenerateNumericCode()
    {
        var value = RandomNumberGenerator.GetInt32(0, 1_000_000);
        return value.ToString($"D{CodeLength}");
    }

    private static string HashCode(string code, Guid userId)
    {
        var raw = $"{userId:N}:{code}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes);
    }
}
