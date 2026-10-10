using Microsoft.AspNetCore.Mvc.RazorPages;
namespace StayWidget.Web.Pages;
public sealed class DemoModel : PageModel
{
    public string Stage { get; private set; } = "Search";
    public void OnGet() { }
    public void OnPostRequest() => Stage="Pending";
    public void OnPostConfirm() => Stage="Confirmed";
}
