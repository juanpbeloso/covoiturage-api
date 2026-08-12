using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SubiteAPI.Exceptions;

namespace SubiteAPI.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>Id del usuario autenticado, extraído del JWT.</summary>
    protected Guid CurrentUserId
    {
        get
        {
            var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(id, out var userId))
            {
                throw new InvalidTokenException("AccessToken");
            }
            return userId;
        }
    }
}
