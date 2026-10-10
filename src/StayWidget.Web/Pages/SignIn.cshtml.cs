using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace StayWidget.Web.Pages;
public sealed class SignInModel(IConfiguration config, IWebHostEnvironment env, BookingService service) : PageModel
{
    public bool Dev => env.IsDevelopment() && config.GetValue<bool>("StayWidget:DevelopmentLogin") && HttpContext.Connection.RemoteIpAddress is { } ip && IPAddress.IsLoopback(ip);
    public bool Enabled => config.GetValue<bool>("Authentication:Enabled");
    public IActionResult OnGet(bool failed = false) => Enabled && !failed ? Challenge(new AuthenticationProperties { RedirectUri = "/Owner" }, "Funtoolio") : Page();
    public async Task<IActionResult> OnPostDevelopment(string owner)
    {
        if (!Dev || owner is not ("Alex" or "Sam")) return NotFound();
        var id = await service.User("urn:staywidget:development", owner, owner);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity([new Claim("staywidget:user", id.ToString()), new Claim(ClaimTypes.Name, owner)], CookieAuthenticationDefaults.AuthenticationScheme)));
        return Redirect("/Owner");
    }
    public IActionResult OnPostLogout() => Enabled
        ? SignOut(new AuthenticationProperties { RedirectUri = "/" }, CookieAuthenticationDefaults.AuthenticationScheme, "Funtoolio")
        : SignOut(new AuthenticationProperties { RedirectUri = "/" }, CookieAuthenticationDefaults.AuthenticationScheme);
}
