using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using StayWidget.Domain;
using StayWidget.Persistence;
namespace StayWidget.Web.Pages.Owner;

public sealed class SettingsModel(StayDb db, BookingService service) : PageModel
{
    [BindProperty] public string PropertyName { get; set; } = "";
    [BindProperty] public string Contact { get; set; } = "";
    [BindProperty] public string Policies { get; set; } = "";
    [BindProperty] public int Revision { get; set; }
    [BindProperty] public bool Published { get; set; }
    [BindProperty] public bool Understood { get; set; }
    public string Zone { get; private set; } = "";
    private Guid Owner => Guid.Parse(User.FindFirstValue("staywidget:user")!);
    public async Task<IActionResult> OnGet()
    {
        var p = await db.Properties.AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == Owner);
        if (p == null) return RedirectToPage("Index");
        PropertyName = p.Name; Contact = p.Contact; Policies = p.Policies; Revision = p.Revision; Published = p.Published; Zone = p.Zone;
        return Page();
    }
    public async Task<IActionResult> OnPost()
    {
        try
        {
            if (!ModelState.IsValid) throw new RuleViolation("Check the required fields.");
            await service.UpdateProperty(Owner, Revision, PropertyName, Contact, Policies, Published, Understood);
            return RedirectToPage("Index");
        }
        catch (RuleViolation e) { Response.StatusCode = 409; ModelState.AddModelError("", e.Message); return Page(); }
    }
}
