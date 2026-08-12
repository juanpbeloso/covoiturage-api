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

public interface IPasswordResetService
{
    Task RequestCodeAsync(ForgotPasswordDto dto);
    Task VerifyCodeAsync(VerifyResetCodeDto dto);
    Task ResetPasswordAsync(ResetPasswordDto dto);
}

public class PasswordResetService : IPasswordResetService
{
    private const int CodeLength = 6;
    private const int ExpireMinutes = 15;
    private const int MaxAttempts = 5;

    private readonly UserManager<User> _userManager;
    private readonly AppDbContext _db;
    private readonly IEmailService _email;
    private readonly EnvialoSimpleOptions _emailOptions;
    private readonly ILogger<PasswordResetService> _logger;

    public PasswordResetService(
        UserManager<User> userManager,
        AppDbContext db,
        IEmailService email,
        IOptions<EnvialoSimpleOptions> emailOptions,
        ILogger<PasswordResetService> logger)
    {
        _userManager = userManager;
        _db = db;
        _email = email;
        _emailOptions = emailOptions.Value;
        _logger = logger;
    }

    public async Task RequestCodeAsync(ForgotPasswordDto dto)
    {
        var email = dto.Email.Trim();
        var user = await _userManager.FindByEmailAsync(email).ConfigureAwait(false);

        if (user == null)
        {
            _logger.LogInformation("Forgot password solicitado para email inexistente.");
            return;
        }

        var now = DateTime.UtcNow;
        var active = await _db.PasswordResetCodes
            .Where(c => c.UserId == user.Id && c.UsedAt == null && c.ExpiresAt > now)
            .ToListAsync()
            .ConfigureAwait(false);

        foreach (var row in active)
        {
            row.UsedAt = now;
        }

        var code = GenerateNumericCode();
        _db.PasswordResetCodes.Add(new PasswordResetCode
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
                "EnvialoSimple deshabilitado. Código de reset para {Email}: {Code}",
                user.Email,
                code);
            return;
        }

        try
        {
            await _email.SendTemplateAsync(
                user.Email!,
                "password-reset-code",
                new Dictionary<string, string>
                {
                    ["name"] = string.IsNullOrWhiteSpace(user.FullName) ? "hola" : user.FullName,
                    ["code"] = code,
                    ["minutes"] = ExpireMinutes.ToString()
                }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo enviar email de reset a {Email}. Código: {Code}", user.Email, code);
            throw new BusinessException(
                "AUTH_EMAIL_SEND",
                "No pudimos enviar el email. Intentá de nuevo en unos minutos.");
        }
    }

    public async Task VerifyCodeAsync(VerifyResetCodeDto dto)
    {
        await GetValidCodeAsync(dto.Email, dto.Code).ConfigureAwait(false);
    }

    public async Task ResetPasswordAsync(ResetPasswordDto dto)
    {
        var (user, entity) = await GetValidCodeAsync(dto.Email, dto.Code).ConfigureAwait(false);

        var token = await _userManager.GeneratePasswordResetTokenAsync(user).ConfigureAwait(false);
        var result = await _userManager.ResetPasswordAsync(user, token, dto.NewPassword).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new BusinessException("AUTH_005", errors);
        }

        entity.UsedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync().ConfigureAwait(false);
    }

    private async Task<(User user, PasswordResetCode entity)> GetValidCodeAsync(string email, string code)
    {
        var user = await _userManager.FindByEmailAsync(email.Trim()).ConfigureAwait(false)
            ?? throw new BusinessException("AUTH_RESET_INVALID", "Código inválido o vencido.");

        var now = DateTime.UtcNow;
        var entity = await _db.PasswordResetCodes
            .Where(c => c.UserId == user.Id && c.UsedAt == null && c.ExpiresAt > now)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (entity == null)
        {
            throw new BusinessException("AUTH_RESET_INVALID", "Código inválido o vencido.");
        }

        if (entity.Attempts >= MaxAttempts)
        {
            entity.UsedAt = now;
            await _db.SaveChangesAsync().ConfigureAwait(false);
            throw new BusinessException("AUTH_RESET_LOCKED", "Demasiados intentos. Pedí un código nuevo.");
        }

        var hash = HashCode(code.Trim(), user.Id);
        var expected = Encoding.UTF8.GetBytes(entity.CodeHash);
        var actual = Encoding.UTF8.GetBytes(hash);
        if (expected.Length != actual.Length
            || !CryptographicOperations.FixedTimeEquals(expected, actual))
        {
            entity.Attempts += 1;
            await _db.SaveChangesAsync().ConfigureAwait(false);
            throw new BusinessException("AUTH_RESET_INVALID", "Código inválido o vencido.");
        }

        return (user, entity);
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
