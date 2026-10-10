namespace StayWidget.Web;
public static class PublicPages
{
    public static readonly Dictionary<string,string> Routes = new(StringComparer.OrdinalIgnoreCase)
    { ["/"]="page-home", ["/Pricing"]="page-pricing", ["/Demo"]="page-demo", ["/Install"]="page-install", ["/Limitations"]="page-limitations" };
    public static bool IsPublic(PathString path) => Routes.ContainsKey(path.Value ?? "");
}
