using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using StayWidget.Domain;
using StayWidget.Persistence;
namespace StayWidget.Web.Pages.Owner;

public sealed class BlockModel(StayDb db, BookingService service, TimeProvider clock) : PageModel
{
    public List<Unit> Units { get; private set; } = [];
    public bool Active { get; private set; } = true;
    [BindProperty] public Guid Id { get; set; }
    [BindProperty] public int? Revision { get; set; }
    [BindProperty] public Guid UnitId { get; set; }
    [BindProperty] public DateOnly Start { get; set; }
    [BindProperty] public DateOnly End { get; set; }
    [BindProperty] public string Reason { get; set; } = "";
    private Guid Owner => Guid.Parse(User.FindFirstValue("staywidget:user")!);
    private async Task<Property?> Load()
    {
        var p = await db.Properties.AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == Owner);
        if (p != null) Units = await db.Units.AsNoTracking().Where(x => x.PropertyId == p.Id).OrderBy(x => x.Name).ToListAsync();
        return p;
    }
    public async Task<IActionResult> OnGet(Guid? id)
    {
        var p = await Load(); if (p == null) return RedirectToPage("Index");
        if (id.HasValue)
        {
            var a = await db.Allocations.AsNoTracking().SingleOrDefaultAsync(x => x.PropertyId == p.Id && x.Id == id && x.ReservationId == null);
            if (a == null) return NotFound();
            Id = a.Id; Revision = a.Revision; UnitId = a.UnitId; Start = a.Start; End = a.End; Reason = a.Reason; Active = a.Active;
        }
        else { Id = Guid.NewGuid(); UnitId = Units[0].Id; Start = StayRules.Today(clock.GetUtcNow(), p.Zone); End = Start.AddDays(1); }
        return Page();
    }
    public async Task<IActionResult> OnPost()
    {
        try
        {
            if (!ModelState.IsValid) throw new RuleViolation("Check the unit, blocked dates and private reason.");
            await service.SaveBlock(Owner, Id, Revision, UnitId, Start, End, Reason);
            return RedirectToPage("Inventory", new { From = Start.ToString("yyyy-MM-dd") });
        }
        catch (RuleViolation e) { Response.StatusCode = 409; ModelState.AddModelError("", e.Message); await Load(); return Page(); }
    }
    public async Task<IActionResult> OnPostRemove()
    {
        ModelState.Clear();
        try
        {
            if (!Revision.HasValue) throw new RuleViolation("Block not found.");
            await service.RemoveBlock(Owner, Id, Revision.Value); return RedirectToPage("Inventory");
        }
        catch (RuleViolation e) { await OnGet(Id); Response.StatusCode = 409; ModelState.AddModelError("", e.Message); return Page(); }
    }
}
