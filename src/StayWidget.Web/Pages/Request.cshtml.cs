using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using StayWidget.Domain;
using StayWidget.Persistence;
using Microsoft.AspNetCore.RateLimiting;
namespace StayWidget.Web.Pages;
[IgnoreAntiforgeryToken]
[EnableRateLimiting("guest")]
public sealed class RequestModel(StayDb db, BookingService service, TimeProvider clock, IDataProtectionProvider protection, SiteOrigin site, ILogger<RequestModel> log) : PageModel
{
    private readonly IDataProtector protector=protection.CreateProtector("StayWidget.GuestForm.v3");
    public bool IsWidget => Request.Path.StartsWithSegments("/w");
    public bool ShowBranding => ProductPlans.ShowBranding(Property);
    public string CalendarMonth => AvailabilityCalendar.Month(StayRules.Today(clock.GetUtcNow(),Property.Zone)).ToString("yyyy-MM-dd");
    [BindProperty(SupportsGet = true)] public string? InstanceId {get;set;}
    [BindProperty] public string? Website {get;set;}
    public Receipt? Receipt {get;private set;}
    public GuestStatus? ReceiptStatus {get;private set;}
    public string ReceiptLink => site.Value + "/GuestStatus#" + Receipt!.Token + (Receipt.ActionToken != null ? "." + Receipt.ActionToken : "");
    public string[] ParentOrigins => new[] { site.Value }.Concat(Property.EmbedAttestedAt != null ? Property.EmbedOrigins : []).ToArray();
    public Property Property {get;private set;}=null!; public Unit Unit {get;private set;}=null!;
    public List<Unit> Units {get;private set;}=[];
    [BindProperty] public Guid UnitId {get;set;}
    [BindProperty] public DateOnly Arrival {get;set;}
    [BindProperty] public DateOnly Departure {get;set;}
    [BindProperty] public int Guests {get;set;}=1;
    [BindProperty] public string GuestName {get;set;}="";
    [BindProperty] public string Phone {get;set;}="";
    [BindProperty] public string Key {get;set;}="";
    [BindProperty] public string FormContext {get;set;}="";
    [BindProperty] public bool Agree {get;set;}
    public bool? Available {get;private set;}
    private async Task<bool> Load(Guid id)
    {
        var p=await db.Properties.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id && x.Published && !x.Suspended);if(p==null)return false;
        Units=await db.Units.AsNoTracking().Where(x=>x.PropertyId==id && x.Enabled).OrderBy(x=>x.Name).ToListAsync();
        if(Units.Count==0)return false;
        Property=p; Unit=Units.FirstOrDefault(x=>x.Id==UnitId)??Units[0];return true;
    }
    public async Task<IActionResult> OnGet(Guid id)
    {
        if(!await Load(id))return NotFound();var today=StayRules.Today(clock.GetUtcNow(),Property.Zone);
        UnitId=Unit.Id;Arrival=today.AddDays(7);Departure=Arrival.AddDays(2);Key=BookingService.Secret();
        if(InstanceId != null && (InstanceId.Length!=32 || !InstanceId.All(Uri.IsHexDigit)))return BadRequest();
        FormContext=protector.Protect($"{id:N}|{Key}|{clock.GetUtcNow().AddHours(2).ToUnixTimeSeconds()}|{Property.TermsVersion}|{clock.GetUtcNow().ToUnixTimeSeconds()}");return Page();
    }
    public async Task<IActionResult> OnPost(Guid id, string action)
    {
        if(!await Load(id))return NotFound();
        try
        {
            if(!EmbedPolicy.SameOrigin(Request,site))throw new RuleViolation("Submit using this property's request page.");
            if(action is not ("Search" or "Submit"))throw new RuleViolation("Choose availability search or request submission.");
            if(InstanceId != null && (InstanceId.Length!=32 || !InstanceId.All(Uri.IsHexDigit)))throw new RuleViolation("Reload this widget and try again.");
            if(action=="Search") { ModelState.Remove(nameof(GuestName)); ModelState.Remove(nameof(Phone)); ModelState.Remove(nameof(Agree)); }
            if(!ModelState.IsValid)throw new RuleViolation("Check the dates and guest count.");
            if(!Units.Any(x=>x.Id==UnitId))throw new RuleViolation("Choose an open unit.");
            var parts=protector.Unprotect(FormContext).Split('|');
            if(parts.Length!=5||parts[0]!=id.ToString("N")||parts[1]!=Key||!long.TryParse(parts[2],out var deadline)||clock.GetUtcNow().ToUnixTimeSeconds()>=deadline||!long.TryParse(parts[4],out var issued)||clock.GetUtcNow().ToUnixTimeSeconds()<issued)throw new RuleViolation("This form expired. Reload the page and try again.");
            if(action=="Search") { if(parts[3]!=Property.TermsVersion)throw new RuleViolation("Property policies changed. Reload the page and review them before requesting."); Available=await service.Available(Unit,Arrival,Departure,Guests); return Page(); }
            if(!string.IsNullOrEmpty(Website))throw new RuleViolation("This submission could not be accepted. Reload or contact the property.");
            if(clock.GetUtcNow().ToUnixTimeSeconds()==issued)log.LogDebug("Anonymous form submitted in its first second; legitimate fast users are not blocked by timing alone.");
            var receipt=await service.Submit(id,new(Unit.Id,Arrival,Departure,Guests,GuestName,Phone,Key,Agree,parts[3],HttpContext.Connection.RemoteIpAddress?.ToString()??"unknown"));
            if(receipt.Token==null) { ModelState.AddModelError("","This request was already received. Use your saved status link or contact the property.");return Page(); }
            if(IsWidget){Receipt=receipt;ReceiptStatus=await service.Status(receipt.Token);return Page();}
            return Redirect("/GuestStatus#"+receipt.Token+(receipt.ActionToken!=null?"."+receipt.ActionToken:""));
        }
        catch(SubmissionLimited e) { ModelState.AddModelError("",e.Message);Response.StatusCode=429;Response.Headers.RetryAfter=e.RetrySeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);return Page(); }
        catch(Exception e) when(e is RuleViolation or System.Security.Cryptography.CryptographicException) { ModelState.AddModelError("",e is RuleViolation?e.Message:"This form is invalid. Reload and try again.");Response.StatusCode=400;return Page(); }
    }
}
