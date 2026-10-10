using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using StayWidget.Persistence;
using StayWidget.Domain;
namespace StayWidget.Web.Pages.Owner;
public sealed class PlansModel(StayDb db, BookingService service, TimeProvider clock) : PageModel
{
    public Property Property { get; private set; } = null!;
    public int Active { get; private set; } public int Stored { get; private set; }
    public string Offer => ProductPlans.Offer(clock.GetUtcNow());
    [BindProperty] public int Revision { get; set; }
    [BindProperty] public string Plan { get; set; } = "Pro";
    [BindProperty] public bool HideBranding { get; set; }
    [BindProperty] public bool Acknowledged { get; set; }
    private Guid Owner => Guid.Parse(User.FindFirstValue("staywidget:user")!);
    private async Task<bool> Load()
    {
        var p = await db.Properties.AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == Owner); if(p == null) return false;
        Property = p; Stored = await db.Units.CountAsync(x=>x.PropertyId==p.Id); Active = await db.Units.CountAsync(x=>x.PropertyId==p.Id && x.Enabled); return true;
    }
    public async Task<IActionResult> OnGet() { if(!await Load()) return RedirectToPage("Index"); Revision=Property.Revision; Plan=Property.Plan; HideBranding=Property.HideBranding; return Page(); }
    public async Task<IActionResult> OnPost()
    {
        try { if(!ModelState.IsValid) throw new RuleViolation("Check the plan preferences."); await service.SetPlan(Owner,Revision,Plan,HideBranding,Acknowledged); return RedirectToPage(); }
        catch(RuleViolation e) { Response.StatusCode=409; ModelState.AddModelError("",e.Message); if(!await Load()) return NotFound(); return Page(); }
    }
}
