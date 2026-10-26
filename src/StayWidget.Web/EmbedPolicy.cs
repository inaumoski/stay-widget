using System.Globalization;
using StayWidget.Domain;

namespace StayWidget.Web;

public sealed class SiteOrigin(string value)
{
    public string Value { get; } = EmbedPolicy.Origin(value, false);
    public string Canonical(string path) => Value + path.TrimEnd('/');
}

public static class EmbedPolicy
{
    public static string Origin(string value, bool development)
    {
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) || !string.IsNullOrEmpty(uri.UserInfo) ||
            uri.AbsolutePath != "/" || uri.Query.Length > 0 || uri.Fragment.Length > 0 ||
            (uri.Scheme != "https" && !(development && uri.Scheme == "http" && uri.IsLoopback)) ||
            string.IsNullOrEmpty(uri.Host) || value.Contains('*') || value.Length > 300 || value.Any(char.IsControl))
            throw new RuleViolation("Use an exact HTTPS origin, such as https://www.example.com, without paths, credentials, wildcards or query strings. HTTP loopback origins are allowed only in Development.");
        return uri.GetLeftPart(UriPartial.Authority).ToLowerInvariant();
    }
    public static string Accent(string value)
    {
        if (value.Length != 7 || value[0] != '#' || !value[1..].All(Uri.IsHexDigit)) throw new RuleViolation("Choose a six-digit hex accent color.");
        double Channel(string s) { var c = int.Parse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255d; return c <= .04045 ? c / 12.92 : Math.Pow((c + .055) / 1.055, 2.4); }
        var light = .2126 * Channel(value[1..3]) + .7152 * Channel(value[3..5]) + .0722 * Channel(value[5..7]);
        if (1.05 / (light + .05) < 4.5) throw new RuleViolation("Choose a darker accent so white button text stays readable (contrast at least 4.5:1).");
        return value.ToUpperInvariant();
    }
    public static bool SameOrigin(HttpRequest request, SiteOrigin site)
    {
        var origin = request.Headers.Origin.ToString();
        // Native no-referrer form navigations can carry Origin: null. Never allow an opaque cross-site context.
        if (origin == "null") return request.Headers["Sec-Fetch-Site"] == "same-origin";
        if (origin.Length > 0) return origin == site.Value;
        return request.Headers["Sec-Fetch-Site"] != "cross-site";
    }
}
