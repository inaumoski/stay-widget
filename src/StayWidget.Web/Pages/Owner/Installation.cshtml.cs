using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using StayWidget.Domain;
using StayWidget.Persistence;
namespace StayWidget.Web.Pages.Owner;

public sealed class InstallationModel(StayDb db, BookingService service, SiteOrigin site, IWebHostEnvironment environment) : PageModel
{
    public Property Property { get; private set; } = null!;
    [BindProperty] public int Revision { get; set; }
    [BindProperty] public string? Origins { get; set; }
    [BindProperty] public string Accent { get; set; } = "#12616A";
    [BindProperty] public bool Attested { get; set; }
    [BindProperty] public bool ShowAvailabilityCalendar { get; set; } = true;
    public string Hosted => site.Value + "/request/" + Property.Id;
    public string Widget => site.Value + "/w/" + Property.Id;
    public string Loader => site.Value + "/embed/v1/loader.js";
    public string LoaderSnippet => $"<div data-staywidget=\"{Property.Id}\">\n  <a href=\"{Hosted}\" target=\"_blank\" rel=\"noopener\">Open reservation request page</a>\n</div>\n<script src=\"{Loader}\" defer></script>";
    public string IframeSnippet => $"<iframe src=\"{Widget}\" title=\"Reservation requests\" width=\"100%\" height=\"860\" referrerpolicy=\"no-referrer\" sandbox=\"allow-scripts allow-same-origin allow-forms allow-popups allow-popups-to-escape-sandbox\"></iframe>\n<a href=\"{Hosted}\" target=\"_blank\" rel=\"noopener\">Open reservation request page</a>";
    private Guid Owner => Guid.Parse(User.FindFirstValue("staywidget:user")!);
    private async Task<bool> Load()
    {
        var p = await db.Properties.AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == Owner); if (p == null) return false;
        Property = p; return true;
    }
    public async Task<IActionResult> OnGet()
    {
        if (!await Load()) return RedirectToPage("Index");
        Revision = Property.Revision; Origins = string.Join('\n', Property.EmbedOrigins); Accent = Property.AccentColor; ShowAvailabilityCalendar = Property.ShowAvailabilityCalendar; return Page();
    }
    public async Task<IActionResult> OnPost()
    {
        try
        {
            if (!ModelState.IsValid) throw new RuleViolation("Check the origins, accent color and acknowledgement.");
            await service.SaveEmbedSettings(Owner, Revision, Origins ?? "", Accent, Attested, environment.IsDevelopment(), ShowAvailabilityCalendar);
            return RedirectToPage();
        }
        catch (RuleViolation e) { Response.StatusCode = 409; ModelState.AddModelError("", e.Message); if (!await Load()) return NotFound(); return Page(); }
    }
    public async Task<IActionResult> OnPostActivate(bool acknowledged)
    {
        try { if(!ModelState.IsValid) throw new RuleViolation("Check the acknowledgement."); await service.AcknowledgeInstallation(Owner,Revision,acknowledged); return RedirectToPage(); }
        catch(RuleViolation e) { Response.StatusCode=409; ModelState.AddModelError("",e.Message); if(!await Load()) return NotFound(); return Page(); }
    }
}
