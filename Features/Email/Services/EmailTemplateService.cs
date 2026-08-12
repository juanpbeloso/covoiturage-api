using System.Text.RegularExpressions;
using SubiteAPI.Features.Email.Domain;

namespace SubiteAPI.Features.Email.Services;

public partial class EmailTemplateService : IEmailTemplateService
{
    private readonly string _templatesPath;

    public EmailTemplateService(IWebHostEnvironment env)
    {
        _templatesPath = Path.Combine(env.ContentRootPath, "EmailTemplates");
    }

    public IReadOnlyCollection<string> ListTemplates()
    {
        if (!Directory.Exists(_templatesPath))
        {
            return Array.Empty<string>();
        }

        return Directory
            .GetFiles(_templatesPath, "*.html", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => n is not null)
            .Cast<string>()
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<RenderedEmail> RenderAsync(
        string templateName,
        IReadOnlyDictionary<string, string> model)
    {
        var path = Path.Combine(_templatesPath, $"{templateName}.html");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Plantilla de email no encontrada: {templateName}", path);
        }

        var html = await File.ReadAllTextAsync(path).ConfigureAwait(false);
        html = ApplyModel(html, model);

        var subject = ExtractSubject(html) ?? $"Subite — {templateName}";
        html = SubjectMetaRegex().Replace(html, string.Empty);

        return new RenderedEmail
        {
            Subject = subject,
            HtmlBody = html.Trim(),
            TextBody = StripHtml(html)
        };
    }

    private static string ApplyModel(string html, IReadOnlyDictionary<string, string> model)
    {
        foreach (var (key, value) in model)
        {
            html = html.Replace($"{{{{{key}}}}}", value, StringComparison.OrdinalIgnoreCase);
        }

        return html;
    }

    private static string? ExtractSubject(string html)
    {
        var match = SubjectMetaRegex().Match(html);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static string StripHtml(string html) =>
        HtmlTagRegex().Replace(html, " ")
            .Replace("&nbsp;", " ", StringComparison.OrdinalIgnoreCase)
            .Trim();

    [GeneratedRegex(@"<!--\s*subject:\s*(.+?)\s*-->", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex SubjectMetaRegex();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex HtmlTagRegex();
}
