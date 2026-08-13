namespace SubiteAPI.Options;

public class AdminOptions
{
    public const string SectionName = "Admin";

    public string Email { get; set; } = "admin@subiteapp.com.ar";
    public string Password { get; set; } = "SubiteAdmin123";
    public string FullName { get; set; } = "Administrador Subite";
}
