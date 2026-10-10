using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Playwright;
using StayWidget.Web;
using Xunit;
namespace StayWidget.Tests;

public sealed class AnalyticsBrowserTests(TestDatabase fixture) : IClassFixture<TestDatabase>
{
    [Fact]
    public async Task PublicViewsAreSanitizedChoicesStopTrafficAndPrivatePagesStayExcluded()
    {
        var root = Environment.GetEnvironmentVariable("STAYWIDGET_ROOT")!;
        const string origin = "https://staywidget.example.test", app = "https://localhost:6286";
        const string website = "dac23110-9d42-48c5-a84c-7fe3f5f5a100";
        var (_, property, _) = await fixture.Property();
        var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Fantoolie/dotnet/dotnet.exe"), $"\"{root}/src/StayWidget.Web/bin/Release/net10.0/StayWidget.Web.dll\"")
        { WorkingDirectory = Path.Combine(root, "src/StayWidget.Web"), UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Testing"; start.Environment["ASPNETCORE_URLS"] = app;
        start.Environment["StayWidget__PublicOrigin"] = origin; start.Environment["ConnectionStrings__StayWidget"] = fixture.Connection;
        start.Environment["StayWidget__DataRoot"] = Path.Combine(root, ".local/analytics-fixture-keys");
        start.Environment["Authentication__Enabled"] = "true"; start.Environment["Authentication__Issuer"] = "https://127.0.0.1:1";
        start.Environment["Authentication__ClientId"] = "synthetic-unused"; start.Environment["Authentication__ClientSecret"] = "synthetic-not-a-provider-credential";
        start.Environment["StayWidget__DevelopmentLogin"] = "false"; start.Environment["Email__Mode"] = "Disabled";
        start.Environment["Analytics__Enabled"] = "true"; start.Environment["Analytics__WebsiteId"] = website;
        using var server = Process.Start(start)!; server.BeginOutputReadLine(); server.BeginErrorReadLine();
        try
        {
            using var http = new HttpClient(new HttpClientHandler { ServerCertificateCustomValidationCallback = (_, _, _, _) => true, AllowAutoRedirect = false }) { BaseAddress = new Uri(app) };
            http.DefaultRequestHeaders.Host = new Uri(origin).Authority;
            bool ready = false;
            for (int i = 0; i < 60; i++) { try { if ((await http.GetAsync("/health/live")).IsSuccessStatusCode) { ready = true; break; } } catch (HttpRequestException) { } await Task.Delay(200); }
            Assert.True(ready);
            foreach (var path in PublicPages.Routes.Keys)
            {
                var response = await http.GetAsync(path); var html = await response.Content.ReadAsStringAsync();
                Assert.Contains("public-analytics.js", html); Assert.Contains(website, html);
                Assert.Contains(PublicAnalytics.ScriptUrl, response.Headers.GetValues("Content-Security-Policy").Single());
                Assert.Contains(PublicAnalytics.CollectorUrl, response.Headers.GetValues("Content-Security-Policy").Single());
            }
            var privatePaths = new[] { "/Owner", "/Owner/Feedback", "/Owner/Plans", "/SignIn?failed=true", "/GuestStatus#synthetic-private", "/demo-widget", "/w/" + property.Id, "/request/" + property.Id, "/Privacy", "/Terms", "/Contact", "/unknown", "/robots.txt", "/sitemap.xml", "/site.css", "/signin-funtoolio", "/signout-callback-oidc" };
            foreach (var path in privatePaths)
            {
                var response = await http.GetAsync(path); var html = await response.Content.ReadAsStringAsync();
                Assert.DoesNotContain("public-analytics.js", html); Assert.DoesNotContain(website, html);
                Assert.DoesNotContain("umami.is", response.Headers.GetValues("Content-Security-Policy").Single());
            }
            foreach (var header in new[] { "DNT", "Sec-GPC" })
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, "/"); request.Headers.Add(header, "1");
                var response = await http.SendAsync(request);
                Assert.DoesNotContain("public-analytics.js", await response.Content.ReadAsStringAsync());
                Assert.DoesNotContain("umami.is", response.Headers.GetValues("Content-Security-Policy").Single());
            }
            var fixturePath = Environment.GetEnvironmentVariable("STAYWIDGET_UMAMI_SCRIPT_FIXTURE");
            var sdk = fixturePath is not null ? await File.ReadAllTextAsync(fixturePath) : """
                const c=document.currentScript;
                window.umami={track:async p=>{p=window[c.dataset.beforeSend]('event',p);if(p)await fetch(c.dataset.hostUrl+'/api/send',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({type:'event',payload:p})});}};
                """;
            using var playwright = await Playwright.CreateAsync(); await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
            var sends = new ConcurrentQueue<IRequest>(); var loads = 0;
            async Task<IBrowserContext> Context(string? initialization = null, bool fail = false)
            {
                var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 390, Height = 844 } });
                if (initialization is not null) await context.AddInitScriptAsync(initialization);
                await context.RouteAsync(origin + "/**", async route =>
                {
                    var uri = new Uri(route.Request.Url); using var response = await http.GetAsync(uri.PathAndQuery);
                    var headers = response.Headers.Concat(response.Content.Headers).Where(h => !new[] { "Content-Length", "Transfer-Encoding", "Content-Encoding" }.Contains(h.Key, StringComparer.OrdinalIgnoreCase)).ToDictionary(h => h.Key, h => string.Join(",", h.Value));
                    await route.FulfillAsync(new() { Status = (int)response.StatusCode, Headers = headers, BodyBytes = await response.Content.ReadAsByteArrayAsync() });
                });
                await context.RouteAsync(PublicAnalytics.ScriptUrl, async r => { Interlocked.Increment(ref loads); if (fail) await r.AbortAsync(); else await r.FulfillAsync(new() { ContentType = "application/javascript", Body = sdk }); });
                await context.RouteAsync("https://gateway.umami.is/**", async r => { sends.Enqueue(r.Request); await r.FulfillAsync(new() { ContentType = "application/json", Body = "{\"cache\":\"disposable-test\"}" }); });
                return context;
            }
            await using var context = await Context(); var page = await context.NewPageAsync();
            foreach (var path in PublicPages.Routes.Keys)
            {
                var previous = sends.Count; var sent = page.WaitForResponseAsync(PublicAnalytics.CollectorUrl);
                await page.GotoAsync(origin + path + "?email=synthetic@example.invalid&token=synthetic-secret#private"); await sent;
                Assert.Equal(previous + 1, sends.Count);
                using var json = JsonDocument.Parse(sends.Last().PostData!); var payload = json.RootElement.GetProperty("payload");
                Assert.Equal(path, payload.GetProperty("url").GetString()); Assert.Equal(website, payload.GetProperty("website").GetString());
                Assert.Equal("staywidget.example.test", payload.GetProperty("hostname").GetString()); Assert.Equal("", payload.GetProperty("referrer").GetString());
                Assert.StartsWith("StayWidget", payload.GetProperty("title").GetString()); Assert.False(payload.TryGetProperty("name", out _));
                Assert.DoesNotContain("synthetic", sends.Last().PostData!); Assert.False(sends.Last().Headers.ContainsKey("referer"));
            }
            Assert.Empty(await context.CookiesAsync(["https://cloud.umami.is", "https://gateway.umami.is"]));
            Assert.False(await page.EvaluateAsync<bool>("Boolean(window.staywidgetUmamiBeforeSend('identify',{id:'synthetic-secret'}))"));
            Assert.False(await page.EvaluateAsync<bool>("Boolean(window.staywidgetUmamiBeforeSend('event',{name:'synthetic-secret'}))"));
            var sanitized = await page.EvaluateAsync<string>("JSON.stringify(window.staywidgetUmamiBeforeSend('event',{url:'/?token=synthetic-secret',title:'synthetic@example.invalid',referrer:'/GuestStatus#synthetic-secret'}))"); Assert.DoesNotContain("synthetic", sanitized);
            await page.GetByText("Website visit settings", new() { Exact = true }).FocusAsync(); await page.Keyboard.PressAsync("Space");
            await page.GetByLabel("Turn off public visit tracking in this browser").CheckAsync(); await page.GetByRole(AriaRole.Button, new() { Name = "Save visit preference" }).ClickAsync();
            await Assertions.Expect(page.GetByRole(AriaRole.Status)).ToContainTextAsync("Preference saved");
            Assert.False(await page.EvaluateAsync<bool>("Boolean(window.staywidgetUmamiBeforeSend('event',{}))"));
            var priorLoads = loads; var priorSends = sends.Count; await page.ReloadAsync(); Assert.Equal(priorLoads, loads); Assert.Equal(priorSends, sends.Count);
            await page.GetByText("Website visit settings", new() { Exact = true }).ClickAsync();
            foreach (var width in new[] { 320, 390 }) { await page.SetViewportSizeAsync(width, 844); Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth<=innerWidth+1")); }
            Directory.CreateDirectory(Path.Combine(root, "artifacts/qa")); await page.Locator(".analytics-settings").ScreenshotAsync(new() { Path = Path.Combine(root, "artifacts/qa/analytics-settings-mobile.png") });
            foreach (var path in new[] { "/Privacy", "/Terms", "/GuestStatus#synthetic-secret", "/demo-widget", "/request/" + property.Id }) await page.GotoAsync(origin + path);
            Assert.Equal(priorLoads, loads); Assert.Equal(priorSends, sends.Count);
            foreach (var signal in new[] { "Object.defineProperty(navigator,'doNotTrack',{get:()=> '1'});", "Object.defineProperty(navigator,'globalPrivacyControl',{get:()=>true});" })
            {
                await using var stopped = await Context(signal); var stoppedPage = await stopped.NewPageAsync(); await stoppedPage.GotoAsync(origin); Assert.Equal(priorLoads, loads); Assert.Equal(priorSends, sends.Count);
            }
            await using var blocked = await Context(fail: true); var blockedPage = await blocked.NewPageAsync(); await blockedPage.GotoAsync(origin);
            await blockedPage.GetByRole(AriaRole.Navigation, new() { Name = "Main", Exact = true }).GetByRole(AriaRole.Link, new() { Name = "Pricing", Exact = true }).ClickAsync();
            await Assertions.Expect(blockedPage).ToHaveURLAsync(origin + "/Pricing"); Assert.Equal(priorSends, sends.Count);
        }
        finally { if (!server.HasExited) server.Kill(true); await server.WaitForExitAsync(); }
    }
}
