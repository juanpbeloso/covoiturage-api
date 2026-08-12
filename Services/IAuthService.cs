using SubiteAPI.DTOs;
using Microsoft.AspNetCore.Http;

namespace SubiteAPI.Services;
public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterDto dto);
    Task<AuthResponseDto> LoginAsync(LoginDto dto);
    Task<AuthResponseDto> GoogleLoginAsync(string idToken);
    Task<AuthResponseDto> AppleLoginAsync(string idToken);
    Task<AuthResponseDto> RefreshTokenAsync(string refreshToken);
    Task<UserDto> GetProfileAsync(Guid userId);
    Task<UserDto> UpdateProfileAsync(Guid userId, UpdateProfileDto dto);
    Task<UserDto> UploadAvatarAsync(Guid userId, IFormFile file);
    Task<PublicDriverProfileDto> GetPublicDriverProfileAsync(Guid userId);
}
