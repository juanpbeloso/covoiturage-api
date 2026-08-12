namespace SubiteAPI.Options;

public class DiditOptions
{
    public const string SectionName = "Didit";

    /// <summary>API key de Didit Console (x-api-key).</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>UUID del workflow KYC publicado.</summary>
    public string WorkflowId { get; set; } = string.Empty;

    /// <summary>Secret del destino de webhooks (HMAC).</summary>
    public string WebhookSecret { get; set; } = string.Empty;

    /// <summary>Deep link de retorno a la app tras completar KYC.</summary>
    public string CallbackUrl { get; set; } = "subite://verification/callback";

    public string BaseUrl { get; set; } = "https://verification.didit.me/v3";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(WorkflowId);
}
