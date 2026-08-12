using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Options;
using SubiteAPI.Exceptions;
using SubiteAPI.Features.Email.Domain;
using SubiteAPI.Features.Email.Options;

namespace SubiteAPI.Features.Email.Services;

public class MailgunEmailService : IEmailService
{
    private readonly HttpClient _http;
    private readonly MailgunOptions _options;
    private readonly IEmailTemplateService _templates;
    private readonly ILogger<MailgunEmailService> _logger;

    public MailgunEmailService(
        HttpClient http,
        IOptions<MailgunOptions> options,
        IEmailTemplateService templates,
        ILogger<MailgunEmailService> logger)
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
                "Mailgun deshabilitado. Email simulado → To: {To}, Subject: {Subject}",
                message.To,
                message.Subject);
            return;
        }

        EnsureConfigured();

        var from = BuildFromAddress();
        using var content = new MultipartFormDataContent
        {
            { new StringContent(from), "from" },
            { new StringContent(message.To), "to" },
            { new StringContent(message.Subject), "subject" },
            { new StringContent(message.HtmlBody, Encoding.UTF8), "html" }
        };

        if (!string.IsNullOrWhiteSpace(message.TextBody))
        {
            content.Add(new StringContent(message.TextBody), "text");
        }

        var url = $"/v3/{_options.Domain}/messages";
        using var response = await _http.PostAsync(url, content, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Mailgun error {Status}: {Body}", (int)response.StatusCode, body);
            throw new ExternalServiceException("Mailgun", "No se pudo enviar el email.");
        }

        _logger.LogInformation("Email enviado vía Mailgun a {To}", message.To);
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

    private string BuildFromAddress()
    {
        var email = string.IsNullOrWhiteSpace(_options.FromEmail)
            ? $"no-reply@{_options.Domain}"
            : _options.FromEmail;
        return $"{_options.FromName} <{email}>";
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey) ||
            string.IsNullOrWhiteSpace(_options.Domain))
        {
            throw new BusinessException(
                "MAIL_001",
                "Mailgun no está configurado. Completá ApiKey y Domain en appsettings.");
        }
    }

    public static void ConfigureHttpClient(HttpClient http, MailgunOptions options)
    {
        http.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"api:{options.ApiKey}"));
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
    }
}
