using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using StayWidget.Domain;
using StayWidget.Persistence;
namespace StayWidget.Web.Pages.Owner;
public sealed class IndexModel(StayDb db, BookingService service, TimeProvider clock, EmailSettings email) : PageModel
{
    public string EmailNotice => email.Notice;
    public Dictionary<Guid,ConfirmationEmail> Emails { get; private set; } = [];
    public Property? Property { get; private set; } public Unit? Unit { get; private set; }
    public List<ReservationRequest> Requests { get; private set; } = [];
    public int RequestPage { get; private set; } = 1; public bool More { get; private set; }
    public DateTimeOffset Now => clock.GetUtcNow();
    private Guid Owner => Guid.Parse(User.FindFirstValue("staywidget:user")!);
    [BindProperty] public string PropertyName { get; set; } = "";
    [BindProperty] public string Contact { get; set; } = "";
    [BindProperty] public string Zone { get; set; } = "Europe/Skopje";
    [BindProperty] public string Policies { get; set; } = "";
    [BindProperty] public string UnitName { get; set; } = "";
    [BindProperty] public int Capacity { get; set; } = 2;
    public async Task OnGet(int requestPage = 1) { RequestPage = Math.Clamp(requestPage, 1, 10000); await Load(); }
    private async Task Load()
    {
        Property = await db.Properties.AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == Owner);
        if (Property == null) return;
        Unit = await db.Units.AsNoTracking().Where(x => x.PropertyId == Property.Id).OrderBy(x => x.Name).FirstAsync();
        Requests = await db.Requests.AsNoTracking().Where(x => x.PropertyId == Property.Id).OrderByDescending(x => x.Created).ThenBy(x => x.Id).Skip((RequestPage - 1) * 100).Take(101).ToListAsync();
        More = Requests.Count > 100; Requests = Requests.Take(100).ToList();
        var ids = Requests.Select(x => x.Id).ToArray();
        Emails = await db.ConfirmationEmails.AsNoTracking().Where(x => x.PropertyId == Property.Id && ids.Contains(x.RequestId)).ToDictionaryAsync(x => x.RequestId);
    }
    public async Task<IActionResult> OnPostSetup(bool understand)
    {
        try { if(!ModelState.IsValid) throw new RuleViolation("Check the required property and unit details."); if(!understand) throw new RuleViolation("Please acknowledge the request-only and manual inventory responsibilities."); await service.Setup(Owner, PropertyName, Contact, Zone, Policies, UnitName, Capacity); return RedirectToPage(); }
        catch(RuleViolation e) { ModelState.AddModelError("", e.Message); await Load(); return Page(); }
    }
    public async Task<IActionResult> OnPostConfirm(Guid id, int revision, decimal? amount, bool agreed)
    {
        foreach(var field in new[]{nameof(PropertyName),nameof(Contact),nameof(Zone),nameof(Policies),nameof(UnitName),nameof(Capacity)}) ModelState.Remove(field);
        try { if(!ModelState.IsValid || !amount.HasValue || !decimal.TryParse(Request.Form["amount"], System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out var parsed) || parsed!=amount.Value) throw new RuleViolation("Enter a valid agreed amount and refresh if the request changed."); await service.Confirm(Owner, id, revision, amount.Value, agreed); return RedirectToPage(); }
        catch(RuleViolation e) { Response.StatusCode=409; ModelState.AddModelError("",e.Message); await Load(); return Page(); }
    }
    public async Task<IActionResult> OnPostDecline(Guid id, int revision, string reason)
    {
        foreach(var field in new[]{nameof(PropertyName),nameof(Contact),nameof(Zone),nameof(Policies),nameof(UnitName),nameof(Capacity)}) ModelState.Remove(field);
        try { if(!ModelState.IsValid) throw new RuleViolation("Check the request and public reason."); await service.Decline(Owner, id, revision, reason ?? ""); return RedirectToPage(); }
        catch (RuleViolation e) { Response.StatusCode = 409; ModelState.AddModelError("", e.Message); await Load(); return Page(); }
    }
    public async Task<IActionResult> OnPostRevoke(Guid id, int revision, bool reviewed)
    {
        foreach(var field in new[]{nameof(PropertyName),nameof(Contact),nameof(Zone),nameof(Policies),nameof(UnitName),nameof(Capacity)}) ModelState.Remove(field);
        try { if(!ModelState.IsValid)throw new RuleViolation("Check the request before revoking links."); await service.RevokeGuestLinks(Owner,id,revision,reviewed);return RedirectToPage(); }
        catch(RuleViolation e){Response.StatusCode=409;ModelState.AddModelError("",e.Message);await Load();return Page();}
    }
}
