using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using SubiteAPI.Exceptions;
using SubiteAPI.Features.Email.Domain;
using SubiteAPI.Features.Email.Options;

namespace SubiteAPI.Features.Email.Services;

public class EnvialoSimpleEmailService : IEmailService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _http;
    private readonly EnvialoSimpleOptions _options;
    private readonly IEmailTemplateService _templates;
    private readonly ILogger<EnvialoSimpleEmailService> _logger;

    public EnvialoSimpleEmailService(
        HttpClient http,
        IOptions<EnvialoSimpleOptions> options,
        IEmailTemplateService templates,
        ILogger<EnvialoSimpleEmailService> logger)
    {
        _http = http;
        _options = options.Value;
        _templates = templates;
        _logger = logger;
    }

    public Task SendTemplateAsync(
        string to,
        string templateName,
        IReadOnlyDictionary<string, string> model,
        CancellationToken cancellationToken = default)
    {
        return SendRenderedAsync(to, templateName, model, cancellationToken);
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation(
                "EnvialoSimple deshabilitado. Email simulado → To: {To}, Subject: {Subject}",
                message.To,
                message.Subject);
            return;
        }

        EnsureConfigured();

        var payload = new SendMailRequest
        {
            From = _options.FromEmail,
            To = message.To,
            Subject = message.Subject,
            Html = message.HtmlBody
        };

        using var content = new StringContent(
            JsonSerializer.Serialize(payload, JsonOptions),
            Encoding.UTF8,
            "application/json");

        using var response = await _http
            .PostAsync("/api/v1/mail/send", content, cancellationToken)
            .ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("EnvialoSimple error {Status}: {Body}", (int)response.StatusCode, body);
            throw new ExternalServiceException("EnvialoSimple", "No se pudo enviar el email.");
        }

        _logger.LogInformation("Email enviado vía EnvialoSimple a {To}", message.To);
    }

    private async Task SendRenderedAsync(
        string to,
        string templateName,
        IReadOnlyDictionary<string, string> model,
        CancellationToken cancellationToken)
    {
        var rendered = await _templates.RenderAsync(templateName, model).ConfigureAwait(false);
        await SendAsync(new EmailMessage
        {
            To = to,
            Subject = rendered.Subject,
            HtmlBody = rendered.HtmlBody,
            TextBody = rendered.TextBody
        }, cancellationToken).ConfigureAwait(false);
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey) ||
            string.IsNullOrWhiteSpace(_options.FromEmail))
        {
            throw new BusinessException(
                "MAIL_001",
                "EnvialoSimple no está configurado. Completá ApiKey y FromEmail en appsettings.");
        }
    }

    public static void ConfigureHttpClient(HttpClient http, EnvialoSimpleOptions options)
    {
        http.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", options.ApiKey);
    }

    private sealed class SendMailRequest
    {
        public string From { get; set; } = string.Empty;
        public string To { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Html { get; set; } = string.Empty;
    }
}
