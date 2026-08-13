using System.ComponentModel.DataAnnotations;

namespace SubiteAPI.DTOs;

public class AdminLoginDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class AdminLoginResponseDto
{
    public string AccessToken { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
}

public class AdminMeDto
{
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
}

public class PlatformSettingsDto
{
    public decimal PlatformCommissionRate { get; set; }
    public decimal PlatformCommissionPercent { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class UpdateCommissionDto
{
    [Range(0, 100)]
    public decimal PlatformCommissionPercent { get; set; }
}
