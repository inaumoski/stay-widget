namespace StayWidget.Web;

public sealed class PublicAnalytics(IConfiguration configuration, SiteOrigin site)
{
    public const string ScriptUrl = "https://cloud.umami.is/script.js";
    public const string CollectorUrl = "https://gateway.umami.is/api/send";
    public bool Enabled { get; } = configuration.GetValue<bool>("Analytics:Enabled");
    public string WebsiteId { get; } = configuration["Analytics:WebsiteId"] ?? "";
    public string Origin => site.Value;
    public bool CanTrack(HttpRequest request, IWebHostEnvironment environment)
    {
        var origin = new Uri(Origin);
        return Enabled && Guid.TryParse(WebsiteId, out var id) && id != Guid.Empty &&
            (environment.IsProduction() || environment.IsEnvironment("Testing")) &&
            !origin.IsLoopback && request.IsHttps &&
            string.Equals(request.Host.Value, origin.Authority, StringComparison.OrdinalIgnoreCase) &&
            PublicPages.Routes.Keys.Contains(request.Path.Value, StringComparer.Ordinal) &&
            request.Headers["DNT"] != "1" && request.Headers["Sec-GPC"] != "1";
    }
}
