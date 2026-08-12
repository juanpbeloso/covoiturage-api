namespace SubiteAPI.Helpers;

public static class PaymentUrlHelper
{
    public static string ResolveReturnBase(string? clientReturnBase, string backendUrl)
    {
        if (IsValidHttpUrl(clientReturnBase))
        {
            return clientReturnBase!.TrimEnd('/');
        }

        if (IsValidHttpUrl(backendUrl))
        {
            return backendUrl.TrimEnd('/');
        }

        throw new InvalidOperationException(
            "Configurá App:BackendUrl (o enviá returnBaseUrl) con una URL http(s) válida para MercadoPago.");
    }

    public static bool IsValidHttpUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        return Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
               !string.IsNullOrEmpty(uri.Host);
    }

    /// <summary>
    /// MercadoPago solo acepta URLs públicas con HTTPS (back_urls, notification_url).
    /// Las URLs HTTP (localhost, IP local) se descartan o rechazan.
    /// </summary>
    public static bool IsHttpsUrl(string? url) =>
        IsValidHttpUrl(url) &&
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps;

    /// <summary>Deep link de la app móvil (ej. subite://payments/return/success).</summary>
    public static bool IsAppDeepLink(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        !string.IsNullOrEmpty(uri.Scheme) &&
        uri.Scheme != Uri.UriSchemeHttp &&
        uri.Scheme != Uri.UriSchemeHttps;

    public static string BuildAppReturnUrl(string frontendBase, string path)
    {
        var raw = frontendBase.Trim().TrimEnd('/');
        var scheme = raw.Contains("://", StringComparison.Ordinal)
            ? raw.Split("://", 2)[0]
            : raw;

        return $"{scheme}://{path.TrimStart('/')}";
    }
}
