using Google.Apis.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using SubiteAPI.Data;
using SubiteAPI.DTOs;
using SubiteAPI.Exceptions;
using SubiteAPI.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace SubiteAPI.Services;

public class AuthService : IAuthService
{
    private static readonly HashSet<string> AllowedAvatarContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/jpg",
        "image/png",
        "image/webp"
    };

    private readonly UserManager<User> _userManager;
    private readonly IJwtService _jwtService;
    private readonly IConfiguration _config;
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly IEmailVerificationService _emailVerification;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<User> userManager,
        IJwtService jwtService,
        IConfiguration config,
        AppDbContext db,
        IWebHostEnvironment env,
        IEmailVerificationService emailVerification,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _jwtService = jwtService;
        _config = config;
        _db = db;
        _env = env;
        _emailVerification = emailVerification;
        _logger = logger;
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto)
    {
        var existingUser = await _userManager.FindByEmailAsync(dto.Email).ConfigureAwait(false);
        if (existingUser != null)
        {
            throw new EmailAlreadyExistsException(dto.Email);
        }

        var user = new User
        {
            Email = dto.Email,
            UserName = dto.Email,
            FullName = dto.FullName,
            PhoneNumber = dto.Phone,
            EmailConfirmed = false
        };

        var result = await _userManager.CreateAsync(user, dto.Password).ConfigureAwait(false);

        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new BusinessException("AUTH_005", errors);
        }

        try
        {
            await _emailVerification.SendCodeAsync(user).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Registro ok pero falló el envío del código a {Email}", user.Email);
        }

        return await CreateAuthResponseAsync(user).ConfigureAwait(false);
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email).ConfigureAwait(false);
        if (user == null)
        {
            throw new InvalidCredentialsException();
        }

        var isValidPassword = await _userManager.CheckPasswordAsync(user, dto.Password).ConfigureAwait(false);
        if (!isValidPassword)
        {
            throw new InvalidCredentialsException();
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user).ConfigureAwait(false);

        return await CreateAuthResponseAsync(user).ConfigureAwait(false);
    }

    public async Task<AuthResponseDto> GoogleLoginAsync(string idToken)
    {
        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { _config["Google:ClientId"] }
            };

            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings).ConfigureAwait(false);

            var user = await _userManager.FindByEmailAsync(payload.Email).ConfigureAwait(false);

            if (user == null)
            {
                user = new User
                {
                    Email = payload.Email,
                    UserName = payload.Email,
                    FullName = payload.Name ?? payload.Email,
                    ProfileImageUrl = payload.Picture,
                    EmailConfirmed = payload.EmailVerified
                };

                var result = await _userManager.CreateAsync(user).ConfigureAwait(false);
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    throw new BusinessException("AUTH_005", errors);
                }
            }

            user.LastLoginAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user).ConfigureAwait(false);

            return await CreateAuthResponseAsync(user).ConfigureAwait(false);
        }
        catch (InvalidJwtException)
        {
            throw new InvalidTokenException("Google");
        }
    }

    public async Task<AuthResponseDto> AppleLoginAsync(string idToken, string? fullName = null)
    {
        try
        {
            var audience = _config["Apple:ClientId"];
            if (string.IsNullOrWhiteSpace(audience))
            {
                throw new InvalidTokenException("Apple");
            }

            var principal = await ValidateAppleIdentityTokenAsync(idToken, audience).ConfigureAwait(false);
            var sub = principal.FindFirstValue("sub")
                ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(sub))
            {
                throw new InvalidTokenException("Apple");
            }

            var email = principal.FindFirstValue("email")
                ?? principal.FindFirstValue(ClaimTypes.Email);

            var existingLogin = await _userManager.FindByLoginAsync("apple", sub).ConfigureAwait(false);
            User? user = existingLogin;

            if (user == null && !string.IsNullOrWhiteSpace(email))
            {
                user = await _userManager.FindByEmailAsync(email).ConfigureAwait(false);
            }

            if (user == null)
            {
                var userEmail = !string.IsNullOrWhiteSpace(email)
                    ? email
                    : $"apple_{sub}@privaterelay.appleid.local";

                user = new User
                {
                    Email = userEmail,
                    UserName = userEmail,
                    FullName = string.IsNullOrWhiteSpace(fullName) ? "Usuario Apple" : fullName.Trim(),
                    EmailConfirmed = true
                };

                var create = await _userManager.CreateAsync(user).ConfigureAwait(false);
                if (!create.Succeeded)
                {
                    var errors = string.Join(", ", create.Errors.Select(e => e.Description));
                    throw new BusinessException("AUTH_005", errors);
                }
            }
            else if (!string.IsNullOrWhiteSpace(fullName)
                     && (string.IsNullOrWhiteSpace(user.FullName) || user.FullName == "Usuario Apple"))
            {
                user.FullName = fullName.Trim();
            }

            var logins = await _userManager.GetLoginsAsync(user).ConfigureAwait(false);
            if (!logins.Any(l => l.LoginProvider == "apple" && l.ProviderKey == sub))
            {
                var addLogin = await _userManager.AddLoginAsync(
                    user,
                    new UserLoginInfo("apple", sub, "Apple")).ConfigureAwait(false);
                if (!addLogin.Succeeded)
                {
                    var errors = string.Join(", ", addLogin.Errors.Select(e => e.Description));
                    throw new BusinessException("AUTH_005", errors);
                }
            }

            user.LastLoginAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user).ConfigureAwait(false);

            return await CreateAuthResponseAsync(user).ConfigureAwait(false);
        }
        catch (BusinessException)
        {
            throw;
        }
        catch (SecurityTokenException)
        {
            throw new InvalidTokenException("Apple");
        }
        catch (Exception)
        {
            throw new InvalidTokenException("Apple");
        }
    }

    private static readonly ConfigurationManager<OpenIdConnectConfiguration> AppleOidc =
        new(
            "https://appleid.apple.com/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever());

    private static async Task<ClaimsPrincipal> ValidateAppleIdentityTokenAsync(
        string idToken,
        string audience)
    {
        var discovery = await AppleOidc.GetConfigurationAsync(CancellationToken.None).ConfigureAwait(false);
        var parameters = new TokenValidationParameters
        {
            ValidIssuer = "https://appleid.apple.com",
            ValidAudiences = new[] { audience },
            IssuerSigningKeys = discovery.SigningKeys,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            NameClaimType = "sub",
        };

        var handler = new JwtSecurityTokenHandler
        {
            MapInboundClaims = false
        };
        return handler.ValidateToken(idToken, parameters, out _);
    }

    public async Task<AuthResponseDto> RefreshTokenAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new InvalidTokenException("RefreshToken");
        }

        var stored = await _db.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == refreshToken)
            .ConfigureAwait(false);

        if (stored == null || !stored.IsActive)
        {
            throw new InvalidTokenException("RefreshToken");
        }

        // Rotación: revocamos el token usado y emitimos uno nuevo.
        stored.RevokedAt = DateTime.UtcNow;

        var response = await CreateAuthResponseAsync(stored.User).ConfigureAwait(false);
        return response;
    }

    public async Task<UserDto> GetProfileAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString()).ConfigureAwait(false)
            ?? throw new UserNotFoundException(userId.ToString());

        return await MapUserAsync(user).ConfigureAwait(false);
    }

    public async Task<UserDto> UpdateProfileAsync(Guid userId, UpdateProfileDto dto)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString()).ConfigureAwait(false)
            ?? throw new UserNotFoundException(userId.ToString());

        if (!string.IsNullOrWhiteSpace(dto.FullName)) user.FullName = dto.FullName.Trim();
        if (dto.Phone != null) user.PhoneNumber = dto.Phone;
        if (dto.Bio != null) user.Bio = dto.Bio;
        if (dto.ProfileImageUrl != null) user.ProfileImageUrl = dto.ProfileImageUrl;

        var result = await _userManager.UpdateAsync(user).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new BusinessException("AUTH_007", errors);
        }

        return await MapUserAsync(user).ConfigureAwait(false);
    }

    public async Task<UserDto> UploadAvatarAsync(Guid userId, IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            throw new BusinessException("AUTH_008", "Seleccioná una imagen válida.", 400);
        }

        if (file.Length > 5 * 1024 * 1024)
        {
            throw new BusinessException("AUTH_009", "La imagen no puede superar 5 MB.", 400);
        }

        var contentType = file.ContentType ?? "";
        if (!AllowedAvatarContentTypes.Contains(contentType))
        {
            throw new BusinessException("AUTH_010", "Formato no soportado. Usá JPG, PNG o WebP.", 400);
        }

        var user = await _userManager.FindByIdAsync(userId.ToString()).ConfigureAwait(false)
            ?? throw new UserNotFoundException(userId.ToString());

        var ext = contentType.Contains("png", StringComparison.OrdinalIgnoreCase) ? ".png"
            : contentType.Contains("webp", StringComparison.OrdinalIgnoreCase) ? ".webp"
            : ".jpg";

        var uploadsDir = Path.Combine(_env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot"), "uploads", "avatars");
        Directory.CreateDirectory(uploadsDir);

        var fileName = $"{userId:N}{ext}";
        var fullPath = Path.Combine(uploadsDir, fileName);

        // Eliminar avatares previos del mismo usuario (otra extensión).
        foreach (var old in Directory.EnumerateFiles(uploadsDir, $"{userId:N}.*"))
        {
            try { File.Delete(old); } catch { /* ignore */ }
        }

        await using (var stream = File.Create(fullPath))
        {
            await file.CopyToAsync(stream).ConfigureAwait(false);
        }

        user.ProfileImageUrl = $"/uploads/avatars/{fileName}";
        var result = await _userManager.UpdateAsync(user).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new BusinessException("AUTH_007", errors);
        }

        return await MapUserAsync(user).ConfigureAwait(false);
    }

    public async Task<PublicDriverProfileDto> GetPublicDriverProfileAsync(Guid userId)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Include(u => u.Vehicle)
            .FirstOrDefaultAsync(u => u.Id == userId)
            .ConfigureAwait(false)
            ?? throw new UserNotFoundException(userId.ToString());

        return MapPublicDriver(user);
    }

    private async Task<AuthResponseDto> CreateAuthResponseAsync(User user)
    {
        var roles = await _userManager.GetRolesAsync(user).ConfigureAwait(false);
        var accessToken = _jwtService.GenerateAccessToken(user, roles);
        var refreshToken = _jwtService.GenerateRefreshToken();
        var refreshExpireDays = int.Parse(_config["Jwt:RefreshExpireDays"] ?? "7");

        _db.RefreshTokens.Add(new RefreshToken
        {
            Token = refreshToken,
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(refreshExpireDays)
        });
        await _db.SaveChangesAsync().ConfigureAwait(false);

        return new AuthResponseDto
        {
            Success = true,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            User = await MapUserAsync(user).ConfigureAwait(false)
        };
    }

    private async Task<UserDto> MapUserAsync(User user)
    {
        var mpConnected = await _db.ConductorMercadoPagos.AsNoTracking()
            .AnyAsync(c => c.ConductorId == user.Id)
            .ConfigureAwait(false);

        return new UserDto
        {
            Id = user.Id,
            Email = user.Email ?? "",
            FullName = user.FullName,
            Phone = user.PhoneNumber,
            ProfileImageUrl = user.ProfileImageUrl,
            IsVerified = user.IsVerified,
            EmailVerified = user.EmailConfirmed,
            PhoneVerified = user.PhoneNumberConfirmed,
            IsDriver = user.IsDriver,
            MercadoPagoConnected = mpConnected,
            Rating = user.Rating,
            ReviewsCount = user.ReviewsCount,
            TripsAsDriver = user.TripsAsDriver,
            TripsAsPassenger = user.TripsAsPassenger
        };
    }

    private static PublicDriverProfileDto MapPublicDriver(User user) => new()
    {
        Id = user.Id,
        FullName = user.FullName,
        Bio = user.Bio,
        ProfileImageUrl = user.ProfileImageUrl,
        IsVerified = user.IsVerified,
        EmailVerified = user.EmailConfirmed,
        PhoneVerified = user.PhoneNumberConfirmed,
        Rating = user.Rating,
        ReviewsCount = user.ReviewsCount,
        TripsAsDriver = user.TripsAsDriver,
        Vehicle = user.Vehicle == null
            ? null
            : new PublicDriverVehicleDto
            {
                Brand = user.Vehicle.Brand,
                Model = user.Vehicle.Model,
                Color = user.Vehicle.Color,
                LicensePlate = user.Vehicle.LicensePlate,
                Year = user.Vehicle.Year,
                ImageUrl = user.Vehicle.ImageUrl
            }
    };
}
