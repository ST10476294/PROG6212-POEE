using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Models;
namespace RaceDay.Api.Controllers;
[Route("api/events")]
public class EventsController(RaceDayDbContext db) : SessionController
{
    /// <summary>Browse upcoming events, optionally filtering by discipline and location.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<EventResponse>),200)]
    public async Task<IActionResult> List([FromQuery] string? discipline, [FromQuery] string? location)
    {
        var q = db.Events.Include(e => e.Categories).Include(e => e.Routes).Where(e => e.EventDate > DateTime.Now);
        if (!string.IsNullOrWhiteSpace(discipline)) q = q.Where(e => e.Discipline == discipline);
        if (!string.IsNullOrWhiteSpace(location)) q = q.Where(e => e.Location.Contains(location));
        return Ok((await q.OrderBy(e => e.EventDate).ToListAsync()).Select(Responses.Event));
    }
    /// <summary>View an event, its category distances, and its routes.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(EventResponse),200)]
    public async Task<IActionResult> Get(int id)
    {
        var e = await db.Events.Include(e => e.Categories).Include(e => e.Routes).SingleOrDefaultAsync(e => e.EventId == id);
        return e == null ? NotFound() : Ok(Responses.Event(e));
    }
    /// <summary>Create a future event owned by the logged-in Organiser.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(EventResponse),201)]
    public async Task<IActionResult> Create(EventRequest r)
    {
        if (RequireSession("Organiser") is { } error) return error;
        var e = new Event { OrganiserId = UserId };
        Apply(e,r); db.Events.Add(e); await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get),new { id=e.EventId },Responses.Event(e));
    }
    /// <summary>Update a future event you own. Other Organisers receive 403.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(EventResponse),200)]
    public async Task<IActionResult> Update(int id, EventRequest r)
    {
        if (RequireSession("Organiser") is { } error) return error;
        var e = await db.Events.Include(x => x.Categories).Include(x => x.Routes).SingleOrDefaultAsync(x => x.EventId == id);
        if (e == null) return NotFound(); if (e.OrganiserId != UserId) return Forbidden();
        if (e.EventDate <= DateTime.Now) return Conflict(new { message = "Past events cannot be rescheduled." });
        Apply(e,r); await db.SaveChangesAsync(); return Ok(Responses.Event(e));
    }
    /// <summary>Delete an owned event with no enrolment history; otherwise return 409.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Delete(int id)
    {
        if (RequireSession("Organiser") is { } error) return error;
        var e = await db.Events.Include(x => x.Categories).Include(x => x.Routes).SingleOrDefaultAsync(x => x.EventId == id);
        if (e == null) return NotFound(); if (e.OrganiserId != UserId) return Forbidden();
        if (await db.Enrolments.AnyAsync(x => x.Category!.EventId == id)) return Conflict(new { message = "Event has enrolment history." });
        db.Categories.RemoveRange(e.Categories); db.Routes.RemoveRange(e.Routes); db.Events.Remove(e);
        try { await db.SaveChangesAsync(); } catch (DbUpdateException) { return Conflict(new { message = "Event is still in use." }); }
        return NoContent();
    }
    /// <summary>View all enrolments and captured results for an event you organise.</summary>
    [HttpGet("{id:int}/enrolments")]
    [ProducesResponseType(typeof(IEnumerable<EnrolmentResponse>),200)]
    public async Task<IActionResult> Enrolments(int id)
    {
        if (RequireSession("Organiser") is { } error) return error;
        var e = await db.Events.FindAsync(id);
        if (e == null) return NotFound(); if (e.OrganiserId != UserId) return Forbidden();
        var rows = await db.Enrolments.Include(x => x.Category)!.ThenInclude(x => x!.Event).Include(x => x.Result)
            .Where(x => x.Category!.EventId == id).ToListAsync();
        return Ok(rows.Select(Responses.Enrolment));
    }
    private static void Apply(Event e, EventRequest r)
    { e.Name=r.Name.Trim(); e.Discipline=r.Discipline; e.EventDate=r.EventDate; e.Location=r.Location.Trim(); e.Description=r.Description.Trim(); }
}
