using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using StayWidget.Persistence;
using StayWidget.Web;

var builder = WebApplication.CreateBuilder(args);
if (builder.Environment.IsDevelopment()) builder.Configuration.AddJsonFile("appsettings.Local.json", true, true).AddEnvironmentVariables().AddCommandLine(args);
var connection = builder.Configuration.GetConnectionString("StayWidget") ?? throw new InvalidOperationException("Configure ConnectionStrings:StayWidget for this project's database.");
builder.Services.AddDbContext<StayDb>(o => o.UseNpgsql(connection));
builder.Services.AddSingleton(TimeProvider.System); builder.Services.AddScoped<BookingService>();
var dataRoot = builder.Configuration["StayWidget:DataRoot"];
if (!builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(dataRoot)) throw new InvalidOperationException("Production requires a private persistent StayWidget:DataRoot.");
builder.Services.AddDataProtection().SetApplicationName("StayWidget").PersistKeysToFileSystem(new DirectoryInfo(dataRoot ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StayWidget", "keys")));
builder.Services.AddRazorPages(o => o.Conventions.AuthorizeFolder("/Owner"));
var auth = builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o => { o.Cookie.Name = "StayWidget.Owner"; o.Cookie.HttpOnly = true; o.Cookie.SecurePolicy = CookieSecurePolicy.Always; o.Cookie.SameSite = SameSiteMode.Lax; o.LoginPath = "/SignIn"; o.ExpireTimeSpan = TimeSpan.FromHours(8); });
var enabled = builder.Configuration.GetValue<bool>("Authentication:Enabled");
if (!builder.Environment.IsDevelopment() && !enabled) throw new InvalidOperationException("Production requires configured Funtoolio authentication.");
if (enabled) auth.AddOpenIdConnect("Funtoolio", o =>
{
    o.Authority = builder.Configuration["Authentication:Issuer"]; o.ClientId = builder.Configuration["Authentication:ClientId"]; o.ClientSecret = builder.Configuration["Authentication:ClientSecret"];
    o.ResponseType = "code"; o.UsePkce = true; o.SaveTokens = false; o.MapInboundClaims = false; o.CallbackPath = "/signin-funtoolio";
    o.Scope.Clear(); o.Scope.Add("openid"); o.Scope.Add("profile"); o.TokenValidationParameters.NameClaimType = "name";
    o.Events.OnRedirectToIdentityProvider = c => { c.ProtocolMessage.SetParameter("acr_values", "google"); c.ProtocolMessage.SetParameter("theme", "light"); return Task.CompletedTask; };
    o.Events.OnTokenValidated = async c =>
    {
        var principal = c.Principal!; var subject = principal.FindFirstValue("sub");
        if (subject == null || !principal.FindAll("amr").Any(x => x.Value == "google")) { c.Fail("Google sign-in is required."); return; }
        var name = principal.FindFirstValue("name") ?? "Owner";
        var user = await c.HttpContext.RequestServices.GetRequiredService<BookingService>().User(c.SecurityToken.Issuer, subject, name);
        c.Principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("staywidget:user", user.ToString()), new Claim(ClaimTypes.Name, name.Length > 120 ? name[..120] : name)], CookieAuthenticationDefaults.AuthenticationScheme));
    };
    o.Events.OnRemoteFailure = c => { c.HandleResponse(); c.Response.Redirect("/SignIn?failed=true"); return Task.CompletedTask; };
});
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(o => o.AddPolicy("guest", c => RateLimitPartition.GetFixedWindowLimiter(c.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })));
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 16 * 1024);
var app = builder.Build();
if (args.Contains("--migrate")) { using var scope = app.Services.CreateScope(); await scope.ServiceProvider.GetRequiredService<StayDb>().Database.MigrateAsync(); return; }
if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/Error"); app.UseHsts(); }
app.UseHttpsRedirection();
app.Use(async (c, next) =>
{
    c.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'";
    c.Response.Headers["X-Frame-Options"] = "DENY"; c.Response.Headers["X-Content-Type-Options"] = "nosniff"; c.Response.Headers["Referrer-Policy"] = "no-referrer";
    c.Response.Headers["Cache-Control"] = "no-store"; c.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
    await next();
});
app.UseStaticFiles(); app.UseRouting(); app.UseRateLimiter(); app.UseAuthentication(); app.UseAuthorization();
app.MapGet("/api/guest/status", async (HttpContext c, BookingService service) =>
{
    var token = c.Request.Headers.Authorization.ToString();
    var status = await service.Status(token.StartsWith("Bearer ", StringComparison.Ordinal) ? token[7..] : "");
    return status == null ? Results.NotFound() : Results.Ok(status);
}).RequireRateLimiting("guest");
app.MapRazorPages(); app.Run();
