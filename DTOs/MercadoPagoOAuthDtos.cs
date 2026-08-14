using System.ComponentModel.DataAnnotations;

namespace SubiteAPI.DTOs;

public class MercadoPagoConnectResponseDto
{
    public string AuthorizationUrl { get; set; } = string.Empty;
}

public class MercadoPagoConnectionStatusDto
{
    public bool Connected { get; set; }
    public string? MpUserId { get; set; }
    public DateTime? ConnectedAt { get; set; }
}

public class MercadoPagoOAuthTokenResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("token_type")]
    public string? TokenType { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("scope")]
    public string? Scope { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("user_id")]
    public long UserId { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("public_key")]
    public string? PublicKey { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("live_mode")]
    public bool LiveMode { get; set; }
}
