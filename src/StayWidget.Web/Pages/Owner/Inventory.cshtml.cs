using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using StayWidget.Domain;
using StayWidget.Persistence;
namespace StayWidget.Web.Pages.Owner;

public sealed class InventoryModel(StayDb db, TimeProvider clock) : PageModel
{
    public Property Property { get; private set; } = null!;
    public List<Unit> Units { get; private set; } = [];
    public List<InventoryAllocation> Allocations { get; private set; } = [];
    public List<Reservation> Reservations { get; private set; } = [];
    public List<ActivityRecord> Activity { get; private set; } = [];
    [BindProperty(SupportsGet = true)] public DateOnly From { get; set; }
    public DateOnly To => From.AddDays(14);
    public bool More { get; private set; }
    private Guid Owner => Guid.Parse(User.FindFirstValue("staywidget:user")!);
    public async Task<IActionResult> OnGet()
    {
        var p = await db.Properties.AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == Owner); if (p == null) return RedirectToPage("Index");
        Property = p;
        if (!ModelState.IsValid) return BadRequest("Use an ISO date for the calendar start.");
        if (From == default) From = StayRules.Today(clock.GetUtcNow(), p.Zone);
        if (From.Year is < 2000 or > 2098) return BadRequest("Calendar dates must be between 2000 and 2098.");
        Units = await db.Units.AsNoTracking().Where(x => x.PropertyId == p.Id).OrderBy(x => x.Name).ToListAsync();
        Allocations = await db.Allocations.AsNoTracking().Where(x => x.PropertyId == p.Id && x.Active && x.Start < To && From < x.End).ToListAsync();
        var allocated = Allocations.Where(x => x.ReservationId.HasValue).Select(x => x.ReservationId!.Value).ToList();
        Reservations = await db.Reservations.AsNoTracking().Where(x => x.PropertyId == p.Id && (x.Arrival < To && From < x.Departure || allocated.Contains(x.Id))).OrderBy(x => x.Arrival).ToListAsync();
        Activity = await db.Activity.AsNoTracking().Where(x => x.PropertyId == p.Id).OrderByDescending(x => x.At).Take(20).ToListAsync();
        return Page();
    }
    public string Night(Unit u, DateOnly date)
    {
        var a = Allocations.SingleOrDefault(x => x.UnitId == u.Id && x.Start <= date && date < x.End);
        if (a?.ReservationId != null)
        {
            var r = Reservations.SingleOrDefault(x => x.Id == a.ReservationId);
            return r != null && date >= r.Departure ? "Turnover" : "Booked";
        }
        if (a != null) return "Blocked";
        return !u.Enabled || !Property.Published || date < u.OpenFrom || date >= u.OpenTo ? "Closed" : "Open";
    }
    public async Task<IActionResult> OnGetExport()
    {
        var p = await db.Properties.AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == Owner); if (p == null) return NotFound();
        var units = await db.Units.AsNoTracking().Where(x => x.PropertyId == p.Id).ToDictionaryAsync(x => x.Id, x => x.Name);
        var rows = await db.Reservations.AsNoTracking().Where(x => x.PropertyId == p.Id).OrderBy(x => x.Arrival).Take(10001).ToListAsync();
        if (rows.Count > 10000) return BadRequest("Export exceeds the safe 10,000-row limit. Contact the operator for a scoped data export; no partial file was produced.");
        return File(CsvExport.Reservations(rows, units), "text/csv; charset=utf-8", "staywidget-private-bookings.csv");
    }
}
