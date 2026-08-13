using SubiteAPI.Models;

namespace SubiteAPI.Services;

public interface IJwtService
{
    string GenerateAccessToken(User user, IEnumerable<string>? roles = null);
    string GenerateRefreshToken();
    bool ValidateRefreshToken(string token);
}
