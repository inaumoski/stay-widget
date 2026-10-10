using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using StayWidget.Domain;
namespace StayWidget.Web.Pages;
[IgnoreAntiforgeryToken] // Stateless simulation: no persisted mutation or cookie authority.
[EnableRateLimiting("guest")]
public sealed class DemoWidgetModel(SiteOrigin site, TimeProvider clock) : PageModel
{
    [BindProperty(SupportsGet=true)] public string? InstanceId { get; set; }
    [BindProperty] public DateOnly Arrival { get; set; }
    [BindProperty] public DateOnly Departure { get; set; }
    [BindProperty] public int Guests { get; set; } = 2;
    public string Stage { get; private set; } = "Search";
    public string Origins => System.Text.Json.JsonSerializer.Serialize(new[]{site.Value});
    public IActionResult OnGet()
    {
        if(!ValidInstance()) return BadRequest(); Arrival=DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime).AddDays(7); Departure=Arrival.AddDays(2); return Page();
    }
    private bool ValidInstance()=>InstanceId==null || InstanceId.Length==32 && InstanceId.All(Uri.IsHexDigit);
    private IActionResult Simulate(string stage)
    {
        if(!ValidInstance() || !EmbedPolicy.SameOrigin(Request,site)) return BadRequest();
        try
        {
            if(!ModelState.IsValid) throw new RuleViolation("Check the dates and guest count.");
            var today=DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
            StayRules.Validate(Arrival,Departure,Guests,2,today,today,today.AddDays(394));
            Stage=stage;
        }
        catch(RuleViolation e){Response.StatusCode=400;ModelState.AddModelError("",e.Message);}
        return Page();
    }
    public IActionResult OnPostRequest()=>Simulate("Pending");
    public IActionResult OnPostConfirm()=>Simulate("Confirmed");
}
