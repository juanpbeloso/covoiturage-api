using SubiteAPI.Features.Email.Domain;

namespace SubiteAPI.Features.Email.Services;

public interface IEmailTemplateService
{
    Task<RenderedEmail> RenderAsync(string templateName, IReadOnlyDictionary<string, string> model);
    IReadOnlyCollection<string> ListTemplates();
}

public interface IEmailService
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
    Task SendTemplateAsync(
        string to,
        string templateName,
        IReadOnlyDictionary<string, string> model,
        CancellationToken cancellationToken = default);
}
