using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using Xunit;
namespace StayWidget.Tests;

public sealed class LogoutBrowserTests(TestDatabase fixture) : IClassFixture<TestDatabase>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LogoutRejectsForgeryClearsSessionAndFinishesOnReloadableGet(bool commonLogin)
    {
        var root = Environment.GetEnvironmentVariable("STAYWIDGET_ROOT")!;
        const string app = "https://localhost:6284", issuer = "https://localhost:6285";
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseUrls(issuer); builder.Logging.ClearProviders();
        await using var provider = builder.Build();
        var logoutCalls = 0;
        provider.MapGet("/.well-known/openid-configuration", () => Results.Json(new
        {
            issuer, authorization_endpoint = issuer + "/authorize", token_endpoint = issuer + "/token",
            jwks_uri = issuer + "/keys", end_session_endpoint = issuer + "/logout",
            response_types_supported = new[] { "code" }, subject_types_supported = new[] { "public" },
            id_token_signing_alg_values_supported = new[] { "RS256" }
        }));
        provider.MapGet("/keys", () => Results.Json(new { keys = Array.Empty<object>() }));
        provider.MapGet("/logout", (HttpContext context) =>
        {
            Assert.Equal("logout-fixture", context.Request.Query["client_id"].ToString());
            Assert.Equal(app + "/signout-callback-oidc", context.Request.Query["post_logout_redirect_uri"].ToString());
            Assert.False(string.IsNullOrEmpty(context.Request.Query["state"]));
            Interlocked.Increment(ref logoutCalls);
            return Results.Redirect(app + "/signout-callback-oidc?state=" + Uri.EscapeDataString(context.Request.Query["state"].ToString()));
        });
        await provider.StartAsync();
        var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Fantoolie/dotnet/dotnet.exe"), $"\"{root}/src/StayWidget.Web/bin/Release/net10.0/StayWidget.Web.dll\"")
        { WorkingDirectory = Path.Combine(root, "src/StayWidget.Web"), UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development"; start.Environment["ASPNETCORE_URLS"] = app;
        start.Environment["StayWidget__PublicOrigin"] = app; start.Environment["ConnectionStrings__StayWidget"] = fixture.Connection;
        start.Environment["Authentication__Enabled"] = commonLogin.ToString(); start.Environment["Authentication__Issuer"] = issuer;
        start.Environment["Authentication__ClientId"] = "logout-fixture"; start.Environment["Authentication__ClientSecret"] = "synthetic-not-a-provider-credential";
        start.Environment["StayWidget__DevelopmentLogin"] = "true"; start.Environment["Email__Mode"] = "Disabled";
        using var server = Process.Start(start)!; server.BeginOutputReadLine(); server.BeginErrorReadLine();
        try
        {
            using var http = new HttpClient(new HttpClientHandler { ServerCertificateCustomValidationCallback = (_, _, _, _) => true });
            bool ready = false;
            for (int i = 0; i < 60; i++) { try { if ((await http.GetAsync(app + "/health/live")).IsSuccessStatusCode) { ready = true; break; } } catch (HttpRequestException) { } await Task.Delay(200); }
            Assert.True(ready);
            var widget = await http.GetAsync(app + "/demo-widget");
            Assert.Contains("form-action 'self';", widget.Headers.GetValues("Content-Security-Policy").Single());
            Assert.DoesNotContain(issuer, widget.Headers.GetValues("Content-Security-Policy").Single());
            using var playwright = await Playwright.CreateAsync(); await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
            await using var context = await browser.NewContextAsync(new() { IgnoreHTTPSErrors = true }); var page = await context.NewPageAsync();
            await page.GotoAsync(app + "/SignIn?failed=true"); await page.GetByRole(AriaRole.Button, new() { Name = "Use Alex" }).ClickAsync();
            await Assertions.Expect(page).ToHaveURLAsync(app + "/Owner");
            await page.Locator("form[action*=Logout] input[name=__RequestVerificationToken]").WaitForAsync(new() { State = WaitForSelectorState.Attached });
            var forged = await page.EvaluateAsync<int>("async()=>{const f=document.querySelector('form[action*=Logout]');const d=new FormData(f);d.delete('__RequestVerificationToken');return(await fetch(f.action,{method:'POST',body:d})).status;}");
            Assert.Equal(400, forged); Assert.Contains(await context.CookiesAsync(), c => c.Name == "StayWidget.Owner");
            string? landingMethod = null;
            page.Request += (_, r) => { if (r.IsNavigationRequest && r.Url == app + "/") landingMethod = r.Method; };
            var logoutResponse = await page.RunAndWaitForResponseAsync(
                () => page.GetByRole(AriaRole.Button, new() { Name = "Sign out", Exact = true }).ClickAsync(),
                r => r.Request.Method == "POST" && new Uri(r.Url).AbsolutePath == "/SignIn");
            Assert.Equal(302, logoutResponse.Status);
            await Assertions.Expect(page).ToHaveURLAsync(app + "/"); Assert.Equal("GET", landingMethod);
            Assert.Equal(commonLogin ? 1 : 0, logoutCalls); Assert.DoesNotContain(await context.CookiesAsync(), c => c.Name == "StayWidget.Owner");
            var reload = await page.ReloadAsync(); Assert.Equal(200, reload!.Status);
            await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Sign in", Exact = true })).ToBeVisibleAsync();
            await page.GotoAsync(app + "/Owner");
            if (commonLogin) Assert.StartsWith(issuer + "/authorize", page.Url);
            else Assert.Contains("SignIn", page.Url);
        }
        finally { if (!server.HasExited) server.Kill(true); await server.WaitForExitAsync(); await provider.StopAsync(); }
    }
}
