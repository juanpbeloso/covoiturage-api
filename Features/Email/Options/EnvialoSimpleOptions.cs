namespace SubiteAPI.Features.Email.Options;

public class EnvialoSimpleOptions
{
    public const string SectionName = "EnvialoSimple";

    public bool Enabled { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public string FromName { get; set; } = "Subite";
    public string FromEmail { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://backend.envialosimple.email";
}
