using Microsoft.AspNetCore.Mvc.RazorPages;
namespace StayWidget.Web.Pages;
public sealed class DemoModel(SiteOrigin site) : PageModel
{
    public string Snippet => $"<div data-staywidget=\"demo\">\n  <a href=\"{site.Value}/demo-widget\" target=\"_blank\" rel=\"noopener\">Open synthetic demo</a>\n</div>\n<script src=\"{site.Value}/embed/v1/loader.js\" defer></script>";
    public string Stage { get; private set; } = "Search";
    public void OnGet() { }
    public void OnPostRequest() => Stage="Pending";
    public void OnPostConfirm() => Stage="Confirmed";
}
