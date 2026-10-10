using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using StayWidget.Domain;
using StayWidget.Persistence;
namespace StayWidget.Web.Pages.Owner;

public sealed class UnitsModel(StayDb db, BookingService service, TimeProvider clock) : PageModel
{
    public List<Unit> Units { get; private set; } = [];
    public int CurrentYear { get; private set; }
    public List<Reservation> ExistingBookings { get; private set; } = [];
    [BindProperty] public Guid Id { get; set; }
    [BindProperty] public int? Revision { get; set; }
    [BindProperty] public string UnitName { get; set; } = "";
    [BindProperty] public int Capacity { get; set; } = 2;
    [BindProperty] public DateOnly OpenFrom { get; set; }
    [BindProperty] public DateOnly OpenTo { get; set; }
    [BindProperty] public int MinNights { get; set; } = 1;
    [BindProperty] public int MaxNights { get; set; } = 28;
    [BindProperty] public int Turnover { get; set; }
    [BindProperty] public bool Enabled { get; set; }
    private Guid Owner => Guid.Parse(User.FindFirstValue("staywidget:user")!);
    private async Task<Property?> Load()
    {
        var p = await db.Properties.AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == Owner);
        if (p != null)
        {
            Units = await db.Units.AsNoTracking().Where(x => x.PropertyId == p.Id).OrderBy(x => x.Name).ToListAsync();
            var today = StayRules.Today(clock.GetUtcNow(), p.Zone);
            CurrentYear = today.Year;
            ExistingBookings = await db.Reservations.AsNoTracking().Where(x => x.PropertyId == p.Id && x.State == "Confirmed" && x.Departure > today).OrderBy(x => x.Arrival).ToListAsync();
        }
        return p;
    }
    public async Task<IActionResult> OnGet(Guid? id)
    {
        var p = await Load(); if (p == null) return RedirectToPage("Index");
        if (id.HasValue)
        {
            var u = Units.SingleOrDefault(x => x.Id == id) ; if (u == null) return NotFound();
            Id = u.Id; Revision = u.Revision; UnitName = u.Name; Capacity = u.Capacity; OpenFrom = u.OpenFrom; OpenTo = u.OpenTo;
            MinNights = u.MinNights; MaxNights = u.MaxNights; Turnover = u.Turnover; Enabled = u.Enabled;
        }
        else { Id = Guid.NewGuid(); OpenFrom = StayRules.Today(clock.GetUtcNow(), p.Zone).AddDays(1); OpenTo = OpenFrom.AddDays(393); }
        return Page();
    }
    public async Task<IActionResult> OnPost()
    {
        try
        {
            if (!ModelState.IsValid) throw new RuleViolation("Check the required unit settings.");
            var u = await service.SaveUnit(Owner, Id, Revision, new(UnitName, Capacity, OpenFrom, OpenTo, MinNights, MaxNights, Turnover, Enabled));
            return RedirectToPage(new { id = u.Id });
        }
        catch (RuleViolation e) { Response.StatusCode = 409; ModelState.AddModelError("", e.Message); await Load(); return Page(); }
    }
}
