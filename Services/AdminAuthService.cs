using Microsoft.AspNetCore.Identity;
using SubiteAPI.DTOs;
using SubiteAPI.Exceptions;
using SubiteAPI.Models;

namespace SubiteAPI.Services;

public interface IAdminAuthService
{
    Task<AdminLoginResponseDto> LoginAsync(AdminLoginDto dto);
    Task<AdminMeDto> GetMeAsync(Guid userId);
}

public class AdminAuthService : IAdminAuthService
{
    private readonly UserManager<User> _userManager;
    private readonly IJwtService _jwtService;

    public AdminAuthService(UserManager<User> userManager, IJwtService jwtService)
    {
        _userManager = userManager;
        _jwtService = jwtService;
    }

    public async Task<AdminLoginResponseDto> LoginAsync(AdminLoginDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email.Trim()).ConfigureAwait(false);
        if (user == null)
        {
            throw new InvalidCredentialsException();
        }

        var validPassword = await _userManager.CheckPasswordAsync(user, dto.Password).ConfigureAwait(false);
        if (!validPassword)
        {
            throw new InvalidCredentialsException();
        }

        if (!await _userManager.IsInRoleAsync(user, AppRoles.Admin).ConfigureAwait(false))
        {
            throw new BusinessException("AUTH_FORBIDDEN", "No tenés permisos de administrador.");
        }

        var roles = await _userManager.GetRolesAsync(user).ConfigureAwait(false);
        var token = _jwtService.GenerateAccessToken(user, roles);

        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user).ConfigureAwait(false);

        return new AdminLoginResponseDto
        {
            AccessToken = token,
            Email = user.Email ?? dto.Email,
            FullName = user.FullName
        };
    }

    public async Task<AdminMeDto> GetMeAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString()).ConfigureAwait(false)
            ?? throw new BusinessException("AUTH_001", "Usuario no encontrado.");

        var roles = await _userManager.GetRolesAsync(user).ConfigureAwait(false);
        return new AdminMeDto
        {
            Email = user.Email ?? "",
            FullName = user.FullName,
            Roles = roles.ToList()
        };
    }
}
