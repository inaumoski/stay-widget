using StayWidget.Web;
using Xunit;
namespace StayWidget.Tests;
public sealed class CanonicalUrlTests
{
    [Fact] public void Published_urls_have_one_case_and_no_trailing_slash()
    {
        var site = new SiteOrigin("https://staywidget.funtoolio.com/");
        Assert.Equal("https://staywidget.funtoolio.com", site.Canonical("/"));
        foreach (var path in PublicPages.Routes.Keys)
        {
            Assert.False(site.Canonical(path).EndsWith('/'));
            Assert.Equal(path, PublicPages.CanonicalPath(path.ToLowerInvariant() + "/"));
        }
        Assert.Null(PublicPages.CanonicalPath("/Owner"));
    }
}
