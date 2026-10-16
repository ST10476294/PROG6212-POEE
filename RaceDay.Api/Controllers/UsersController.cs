using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
namespace RaceDay.Api.Controllers;
[Route("api/users")]
public class UsersController(RaceDayDbContext db) : SessionController
{
    /// <summary>View your own profile. Password hashes are never returned.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ProfileResponse),200)]
    public async Task<IActionResult> Get(int id)
    {
        if (RequireSession() is { } error) return error;
        if (id != UserId) return Forbidden();
        var u = await db.Users.FindAsync(id);
        return u == null ? NotFound() : Ok(new ProfileResponse(u.UserId,u.FullName,u.Email,u.Role));
    }
    /// <summary>Update your own full name and email; role changes are not permitted.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ProfileResponse),200)]
    public async Task<IActionResult> Update(int id, UpdateProfileRequest request)
    {
        if (RequireSession() is { } error) return error;
        if (id != UserId) return Forbidden();
        var u = await db.Users.FindAsync(id);
        if (u == null) return NotFound();
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(x => x.Email == email && x.UserId != id)) return Conflict(new { message = "Email already registered." });
        u.FullName = request.FullName.Trim(); u.Email = email;
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException) { return Conflict(new { message = "Profile update conflicts with existing data." }); }
        return Ok(new ProfileResponse(u.UserId,u.FullName,u.Email,u.Role));
    }
    /// <summary>View your own enrolment history, including event details and personal results.</summary>
    [HttpGet("{id:int}/enrolments")]
    [ProducesResponseType(typeof(IEnumerable<EnrolmentResponse>),200)]
    public async Task<IActionResult> Enrolments(int id)
    {
        if (RequireSession("Participant") is { } error) return error;
        if (id != UserId) return Forbidden();
        var rows = await db.Enrolments.Include(e => e.Category)!.ThenInclude(c => c!.Event).Include(e => e.Result)
            .Where(e => e.ParticipantId == id).ToListAsync();
        return Ok(rows.Select(Responses.Enrolment));
    }
}
