namespace SubiteAPI.Features.Email.Options;

public class MailgunOptions
{
    public const string SectionName = "Mailgun";

    public bool Enabled { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string FromName { get; set; } = "Subite";
    public string FromEmail { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.mailgun.net";
}
