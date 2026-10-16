using Microsoft.AspNetCore.Mvc;
namespace RaceDay.Api.Controllers;
[ApiController]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
[ProducesResponseType(StatusCodes.Status409Conflict)]
public abstract class SessionController : ControllerBase
{
    protected int UserId => HttpContext.Session.GetInt32("UserId") ?? 0;
    protected IActionResult? RequireSession(string? role = null)
    {
        if (UserId == 0) return Unauthorized(new { message = "Log in first." });
        if (role != null && HttpContext.Session.GetString("Role") != role)
            return StatusCode(403, new { message = "Your role cannot perform this action." });
        return null;
    }
    protected IActionResult Forbidden() => StatusCode(403, new { message = "This record belongs to another user." });
}
