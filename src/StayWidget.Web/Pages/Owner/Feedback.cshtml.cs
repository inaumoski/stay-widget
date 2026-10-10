using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StayWidget.Domain;
namespace StayWidget.Web.Pages.Owner;
public sealed class FeedbackModel(FeedbackService feedback) : PageModel
{
    [BindProperty] public Guid Key { get; set; } = Guid.NewGuid();
    [BindProperty] public string Body { get; set; } = "";
    [BindProperty] public string? Contact { get; set; }
    [BindProperty(SupportsGet=true)] public string Kind { get; set; } = "Feedback";
    [BindProperty] public bool Acknowledged { get; set; }
    public bool Saved { get; private set; }
    public IActionResult OnGet() { if(Kind is not ("Feedback" or "Custom")) return BadRequest(); return Page(); }
    public async Task<IActionResult> OnPost()
    {
        try
        {
            if(!ModelState.IsValid) throw new RuleViolation("Check your submission.");
            await feedback.Submit(Guid.Parse(User.FindFirstValue("staywidget:user")!),Key,Body,Kind,Contact ?? "",Acknowledged);
            Saved=true; Body=""; Contact=""; return Page();
        }
        catch(RuleViolation e) { Response.StatusCode=409; ModelState.AddModelError("",e.Message); return Page(); }
    }
}
