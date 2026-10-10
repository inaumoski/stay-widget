using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using StayWidget.Persistence;
using StayWidget.Web;

var builder = WebApplication.CreateBuilder(args);
if (builder.Environment.IsDevelopment()) builder.Configuration.AddJsonFile("appsettings.Local.json", true, true).AddEnvironmentVariables().AddCommandLine(args);
var connection = builder.Configuration.GetConnectionString("StayWidget") ?? throw new InvalidOperationException("Configure ConnectionStrings:StayWidget for this project's database.");
builder.Services.AddDbContext<StayDb>(o => o.UseNpgsql(connection));
builder.Services.AddSingleton(TimeProvider.System); builder.Services.AddScoped<BookingService>();
builder.Services.AddSingleton<SubmissionGuard>();
var publicOrigin = builder.Configuration["StayWidget:PublicOrigin"];
if (!builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(publicOrigin)) throw new InvalidOperationException("Production requires the canonical HTTPS StayWidget:PublicOrigin.");
builder.Services.AddSingleton(new SiteOrigin(publicOrigin ?? "https://localhost:7259"));
builder.Services.AddHttpContextAccessor(); builder.Services.AddSingleton<AggregateMetrics>(); builder.Services.AddScoped<FeedbackService>();
if(builder.Configuration.GetValue("StayWidget:MaintenanceEnabled",true)) builder.Services.AddHostedService<ProductMaintenanceWorker>();
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    // One explicit loopback reverse proxy; no forwarded host or trust-all networks.
    o.ForwardedHeaders=ForwardedHeaders.XForwardedFor|ForwardedHeaders.XForwardedProto;
    o.ForwardLimit=1; o.KnownIPNetworks.Clear(); o.KnownProxies.Clear();
    o.KnownProxies.Add(IPAddress.Loopback); o.KnownProxies.Add(IPAddress.IPv6Loopback);
});
if (builder.Configuration.GetValue("StayWidget:MaintenanceEnabled", true)) builder.Services.AddHostedService<MaintenanceWorker>();
var dataRoot = builder.Configuration["StayWidget:DataRoot"];
if (!builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(dataRoot)) throw new InvalidOperationException("Production requires a private persistent StayWidget:DataRoot.");
builder.Services.AddDataProtection().SetApplicationName("StayWidget").PersistKeysToFileSystem(new DirectoryInfo(dataRoot ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StayWidget", "keys")));
builder.Services.AddRazorPages(o => { o.Conventions.AuthorizeFolder("/Owner"); o.Conventions.AddPageRoute("/Request", "/w/{id:guid}"); });
var auth = builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o => { o.Cookie.Name = "StayWidget.Owner"; o.Cookie.HttpOnly = true; o.Cookie.SecurePolicy = CookieSecurePolicy.Always; o.Cookie.SameSite = SameSiteMode.Lax; o.LoginPath = "/SignIn"; o.ExpireTimeSpan = TimeSpan.FromHours(8); });
var enabled = builder.Configuration.GetValue<bool>("Authentication:Enabled");
if (!builder.Environment.IsDevelopment() && !enabled) throw new InvalidOperationException("Production requires configured Funtoolio authentication.");
if (!builder.Environment.IsDevelopment() && builder.Configuration.GetValue("StayWidget:DevelopmentLogin",false)) throw new InvalidOperationException("Development login must be disabled outside Development.");
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
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.OnRejected = async (c, token) => { c.HttpContext.Response.Headers.RetryAfter = "60"; c.HttpContext.Response.ContentType = "text/plain"; await c.HttpContext.Response.WriteAsync("Too many requests. Retry in one minute, use your saved status link or contact the property. No new reservation request was created by this rejected attempt.", token); };
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(c =>
        c.Request.Path.StartsWithSegments("/w") || c.Request.Path.StartsWithSegments("/request") || c.Request.Path.StartsWithSegments("/api/guest") || c.Request.Path.StartsWithSegments("/api/widget")
        ? RateLimitPartition.GetFixedWindowLimiter("guest-global", _ => new FixedWindowRateLimiterOptions { PermitLimit = 500, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })
        : RateLimitPartition.GetNoLimiter("owner-and-static"));
    o.AddPolicy("guest", c => RateLimitPartition.GetFixedWindowLimiter(c.RequestServices.GetRequiredService<SubmissionGuard>().Identity(c.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 64 * 1024);
var app = builder.Build();
if (args.Contains("--migrate")) { using var scope = app.Services.CreateScope(); await scope.ServiceProvider.GetRequiredService<StayDb>().Database.MigrateAsync(); return; }
if (builder.Configuration.GetValue("StayWidget:BehindLoopbackProxy",false)) app.UseForwardedHeaders();
if(!app.Environment.IsDevelopment()) app.Use(async(c,next)=>
{
    var origin=new Uri(c.RequestServices.GetRequiredService<SiteOrigin>().Value);
    if(!string.Equals(c.Request.Host.Host,origin.Host,StringComparison.OrdinalIgnoreCase) || c.Request.Host.Port.GetValueOrDefault(c.Request.IsHttps?443:80)!=origin.Port) { c.Response.StatusCode=400; return; }
    await next();
});
if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/Error"); app.UseHsts(); }
app.UseHttpsRedirection();
app.Use(async (c, next) =>
{
    const string ancestors = "'none'";
    c.Response.Headers["Content-Security-Policy"] = $"default-src 'self'; script-src 'self'; style-src 'self'; frame-ancestors {ancestors}; base-uri 'none'; form-action 'self'; object-src 'none'";
    if (ancestors == "'none'") c.Response.Headers["X-Frame-Options"] = "DENY";
    c.Response.Headers["X-Content-Type-Options"] = "nosniff"; c.Response.Headers["Referrer-Policy"] = "no-referrer";
    c.Response.Headers["Cache-Control"] = "no-store";
    if(!PublicPages.IsPublic(c.Request.Path)) c.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
    await next();
    if(c.Request.Method=="GET" && c.Response.StatusCode==200)
    {
        var metrics=c.RequestServices.GetRequiredService<AggregateMetrics>();
        if(PublicPages.Routes.TryGetValue(c.Request.Path.Value??"",out var e)) metrics.Record(e);
        else if(c.Request.Path.StartsWithSegments("/w")) metrics.Record("widget-view");
        else if(c.Request.Path.StartsWithSegments("/request")) metrics.Record("hosted-view");
    }
});
app.UseStaticFiles(new StaticFileOptions { OnPrepareResponse = c => { if (c.Context.Request.Path == "/embed/v1/loader.js") c.Context.Response.Headers.CacheControl = "public, max-age=300, must-revalidate"; } });
app.UseRouting(); app.UseRateLimiter();
app.Use(async (c, next) =>
{
    var ancestors = "'none'";
    var segments = c.Request.Path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries) ?? [];
    if (segments.Length == 2 && segments[0] == "w" && Guid.TryParse(segments[1], out var propertyId))
    {
        var db = c.RequestServices.GetRequiredService<StayDb>();
        var property = await db.Properties.AsNoTracking().SingleOrDefaultAsync(x => x.Id == propertyId && x.Published && !x.Suspended);
        if (property != null) ancestors = "'self'" + (property.EmbedAttestedAt != null ? " " + string.Join(" ", property.EmbedOrigins) : "");
    }
    if (ancestors != "'none'")
    {
        c.Response.Headers["Content-Security-Policy"] = $"default-src 'self'; script-src 'self'; style-src 'self'; frame-ancestors {ancestors}; base-uri 'none'; form-action 'self'; object-src 'none'";
        c.Response.Headers.Remove("X-Frame-Options");
    }
    await next();
});
app.UseAuthentication(); app.UseAuthorization();
app.MapGet("/api/guest/status", async (HttpContext c, BookingService service) =>
{
    var token = c.Request.Headers.Authorization.ToString();
    var status = await service.Status(token.StartsWith("Bearer ", StringComparison.Ordinal) ? token[7..] : "");
    return status == null ? Results.NotFound() : Results.Ok(status);
}).RequireRateLimiting("guest");
foreach (var action in new[] { "withdraw", "request-cancellation" })
    app.MapPost("/api/guest/" + action, async (GuestActionInput input, HttpContext c, BookingService service, SiteOrigin site) =>
    {
        if (!EmbedPolicy.SameOrigin(c.Request, site)) return Results.BadRequest(new { message = "Use the private guest page to confirm this action." });
        var bearer = c.Request.Headers.Authorization.ToString();
        try { await service.GuestAction(bearer.StartsWith("Bearer ", StringComparison.Ordinal) ? bearer[7..] : "", action, input.Confirmed); return Results.Ok(new { message = "Action recorded. Refreshing the current status." }); }
        catch (GuestLinkUnavailable) { return Results.NotFound(); }
        catch (StayWidget.Domain.RuleViolation e) { return Results.Conflict(new { message = e.Message }); }
    }).RequireRateLimiting("guest");
app.MapGet("/widget-theme/{id:guid}.css", async (Guid id, StayDb db) =>
{
    var color = await db.Properties.AsNoTracking().Where(x => x.Id == id && x.Published && !x.Suspended).Select(x => x.AccentColor).SingleOrDefaultAsync();
    return color == null ? Results.NotFound() : Results.Text($":root{{--accent:{EmbedPolicy.Accent(color)};}}", "text/css");
});
app.MapGet("/sitemap.xml",(SiteOrigin site)=>Results.Text("<?xml version=\"1.0\" encoding=\"utf-8\"?><urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">"+string.Join("",PublicPages.Routes.Keys.Select(x=>"<url><loc>"+System.Security.SecurityElement.Escape(site.Value+x)+"</loc></url>"))+"</urlset>","application/xml"));
app.MapGet("/robots.txt",(SiteOrigin site)=>Results.Text("User-agent: *\nDisallow: /Owner\nDisallow: /GuestStatus\nDisallow: /w/\nDisallow: /request/\nDisallow: /api/\nSitemap: "+site.Value+"/sitemap.xml\n","text/plain"));
app.MapGet("/go/{destination}",(string destination,AggregateMetrics metrics)=>
{
    var url=destination switch { "product"=>"https://staywidget.funtoolie.com/", "portfolio"=>"https://funtoolio.com/", _=>null };
    if(url==null) return Results.NotFound(); metrics.Record("attribution-"+destination); return Results.Redirect(url);
}).RequireRateLimiting("guest");
app.MapGet("/health/live",()=>Results.Text("live"));
app.MapGet("/health/ready",async(StayDb db,HttpContext c)=>
{
    if(c.Connection.RemoteIpAddress is not {} ip || !IPAddress.IsLoopback(ip)) return Results.NotFound();
    using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(3));
    try { await db.Database.ExecuteSqlRawAsync("SELECT 1",timeout.Token); var applied=await db.Database.GetAppliedMigrationsAsync(timeout.Token); return applied.Contains(db.Database.GetMigrations().Last())?Results.Text("ready"):Results.StatusCode(503); }
    catch(Exception) { return Results.StatusCode(503); }
});
app.MapPublicWidget(); app.MapRazorPages(); app.Run();
public sealed record GuestActionInput(bool Confirmed);
