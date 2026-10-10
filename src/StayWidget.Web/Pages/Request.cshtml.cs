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
public sealed class RequestModel(StayDb db, BookingService service, TimeProvider clock, IDataProtectionProvider protection) : PageModel
{
    private readonly IDataProtector protector=protection.CreateProtector("StayWidget.GuestForm.v1");
    public Property Property {get;private set;}=null!; public Unit Unit {get;private set;}=null!;
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
        var p=await db.Properties.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id);if(p==null)return false;
        Property=p; Unit=await db.Units.AsNoTracking().SingleAsync(x=>x.PropertyId==id);return true;
    }
    public async Task<IActionResult> OnGet(Guid id)
    {
        if(!await Load(id))return NotFound();var today=StayRules.Today(clock.GetUtcNow(),Property.Zone);
        Arrival=today.AddDays(7);Departure=Arrival.AddDays(2);Key=BookingService.Secret();
        FormContext=protector.Protect($"{id:N}|{Key}|{clock.GetUtcNow().AddHours(2).ToUnixTimeSeconds()}");return Page();
    }
    public async Task<IActionResult> OnPost(Guid id, string action)
    {
        if(!await Load(id))return NotFound();
        try
        {
            if(action=="Search") { ModelState.Remove(nameof(GuestName)); ModelState.Remove(nameof(Phone)); ModelState.Remove(nameof(Agree)); }
            if(!ModelState.IsValid)throw new RuleViolation("Check the dates and guest count.");
            var parts=protector.Unprotect(FormContext).Split('|');
            if(parts.Length!=3||parts[0]!=id.ToString("N")||parts[1]!=Key||!long.TryParse(parts[2],out var deadline)||clock.GetUtcNow().ToUnixTimeSeconds()>=deadline)throw new RuleViolation("This form expired. Reload the page and try again.");
            if(action=="Search") { Available=await service.Available(Unit,Arrival,Departure,Guests); return Page(); }
            var receipt=await service.Submit(id,new(Unit.Id,Arrival,Departure,Guests,GuestName,Phone,Key,Agree));
            if(receipt.Token==null) { ModelState.AddModelError("","This request was already received. Use your saved status link or contact the property.");return Page(); }
            return Redirect("/GuestStatus#"+receipt.Token);
        }
        catch(Exception e) when(e is RuleViolation or System.Security.Cryptography.CryptographicException) { ModelState.AddModelError("",e is RuleViolation?e.Message:"This form is invalid. Reload and try again.");Response.StatusCode=400;return Page(); }
    }
}
