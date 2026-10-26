namespace StayWidget.Web;
public static class PublicPages
{
    public static readonly Dictionary<string,string> Routes = new(StringComparer.OrdinalIgnoreCase)
    { ["/"]="page-home", ["/Pricing"]="page-pricing", ["/Demo"]="page-demo", ["/Install"]="page-install", ["/Limitations"]="page-limitations" };
    public static string? CanonicalPath(PathString path) => Routes.Keys.FirstOrDefault(route =>
        route.TrimEnd('/').Equals((path.Value ?? "").TrimEnd('/'), StringComparison.OrdinalIgnoreCase));
    public static bool IsPublic(PathString path) => CanonicalPath(path) is not null;
}
