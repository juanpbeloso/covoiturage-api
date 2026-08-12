using Microsoft.AspNetCore.Mvc;
using SubiteAPI.Features.Email.Services;

namespace SubiteAPI.Features.Email.Controllers;

/// <summary>Solo desarrollo: previsualizar y enviar emails de prueba.</summary>
[ApiController]
[Route("api/dev/emails")]
[Tags("Email (Dev)")]
public class EmailDevController : ControllerBase
{
    private readonly IEmailTemplateService _templates;
    private readonly IEmailService _emailService;
    private readonly IWebHostEnvironment _env;

    public EmailDevController(
        IEmailTemplateService templates,
        IEmailService emailService,
        IWebHostEnvironment env)
    {
        _templates = templates;
        _emailService = emailService;
        _env = env;
    }

    /// <summary>Lista plantillas HTML disponibles en /EmailTemplates.</summary>
    [HttpGet("templates")]
    [ProducesResponseType(typeof(IReadOnlyCollection<string>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyCollection<string>> ListTemplates()
    {
        if (!IsDev()) return NotFound();
        return Ok(_templates.ListTemplates());
    }

    /// <summary>Renderiza HTML en el navegador sin enviar (ideal para iterar diseño).</summary>
    [HttpGet("preview/{templateName}")]
    [Produces("text/html")]
    public async Task<IActionResult> Preview(string templateName)
    {
        if (!IsDev()) return NotFound();

        var rendered = await _templates.RenderAsync(templateName, SampleModel()).ConfigureAwait(false);
        return Content(rendered.HtmlBody, "text/html; charset=utf-8");
    }

    /// <summary>Envía el email de bienvenida (GET rápido desde Swagger o navegador).</summary>
    [HttpGet("send-welcome")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SendWelcome([FromQuery] string to, [FromQuery] string? name = null)
    {
        if (!IsDev()) return NotFound();
        if (string.IsNullOrWhiteSpace(to)) return BadRequest(new { error = "Parámetro 'to' requerido (email del destinatario)." });

        var model = SampleModel();
        if (!string.IsNullOrWhiteSpace(name))
        {
            model["FullName"] = name;
        }

        await _emailService.SendTemplateAsync(to.Trim(), "welcome", model).ConfigureAwait(false);
        return Ok(new { message = $"Email de bienvenida enviado a {to.Trim()}", template = "welcome" });
    }

    /// <summary>Envía un email de prueba vía EnvialoSimple.</summary>
    [HttpPost("send-test")]
    public async Task<IActionResult> SendTest([FromBody] SendTestEmailDto dto)
    {
        if (!IsDev()) return NotFound();

        var model = new Dictionary<string, string>(SampleModel(), StringComparer.OrdinalIgnoreCase);
        if (dto.Variables != null)
        {
            foreach (var (key, value) in dto.Variables)
            {
                model[key] = value;
            }
        }

        await _emailService.SendTemplateAsync(dto.To, dto.Template, model).ConfigureAwait(false);
        return Ok(new { message = $"Email '{dto.Template}' enviado a {dto.To}" });
    }

    private bool IsDev() => _env.IsDevelopment();

    private static Dictionary<string, string> SampleModel() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["FullName"] = "Juan Beloso",
        ["OriginCity"] = "Junín",
        ["DestinationCity"] = "Retiro",
        ["DepartureDate"] = "15 Jul 2026 · 08:00 hs",
        ["PricePerSeat"] = "$15.255",
        ["AppUrl"] = "subite://",
        ["Year"] = DateTime.UtcNow.Year.ToString()
    };
}

public class SendTestEmailDto
{
    public string To { get; set; } = string.Empty;
    public string Template { get; set; } = "welcome";
    public Dictionary<string, string>? Variables { get; set; }
}
