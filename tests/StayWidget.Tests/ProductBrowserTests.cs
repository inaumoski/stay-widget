using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using StayWidget.Web;
using Xunit;
namespace StayWidget.Tests;
public sealed class ProductBrowserTests(TestDatabase fixture):IClassFixture<TestDatabase>
{
    [Fact] public async Task PublicDemoOwnerPlansFeedbackAndPrivateExclusionsWorkOnMobile()
    {
        var root=Environment.GetEnvironmentVariable("STAYWIDGET_ROOT")!;const string app="https://localhost:6273";
        Guid ownerId;Guid propertyId;
        await using(var db=fixture.Db()){var s=fixture.Service(db);ownerId=await s.User("urn:staywidget:development","Alex","Alex");var p=await s.Setup(ownerId,"Product test cottage","+389 70 000 000","Europe/Skopje","Please agree price and terms directly before confirmation.","Cottage",2);propertyId=p.Id;}
        var start=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Fantoolie/dotnet/dotnet.exe"),$"\"{root}/src/StayWidget.Web/bin/Release/net10.0/StayWidget.Web.dll\"")
        {WorkingDirectory=Path.Combine(root,"src/StayWidget.Web"),UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        start.Environment["ASPNETCORE_ENVIRONMENT"]="Development";start.Environment["ASPNETCORE_URLS"]=app;start.Environment["StayWidget__PublicOrigin"]=app;start.Environment["ConnectionStrings__StayWidget"]=fixture.Connection;start.Environment["Authentication__Enabled"]="false";start.Environment["StayWidget__DevelopmentLogin"]="true";start.Environment["StayWidget__AggregateMetrics"]="false";
        using var server=Process.Start(start)!;server.BeginOutputReadLine();server.BeginErrorReadLine();
        try
        {
            using var http=new HttpClient(new HttpClientHandler{ServerCertificateCustomValidationCallback=(_,_,_,_)=>true});bool ready=false;
            for(int i=0;i<60;i++){try{if((await http.GetAsync(app)).IsSuccessStatusCode){ready=true;break;}}catch(HttpRequestException){}await Task.Delay(200);}Assert.True(ready);
            Assert.Equal("ready",await http.GetStringAsync(app+"/health/ready"));
            using var pw=await Playwright.CreateAsync();await using var browser=await pw.Chromium.LaunchAsync(new(){Headless=true});
            await using var guest=await browser.NewContextAsync(new(){IgnoreHTTPSErrors=true,ViewportSize=new(){Width=390,Height=844}});var page=await guest.NewPageAsync();
            var external=new List<string>();page.Request+=(_,r)=>{if(!r.Url.StartsWith(app))external.Add(r.Url);};
            foreach(var path in PublicPages.Routes.Keys)
            {
                var response=await page.GotoAsync(app+path);Assert.Equal(200,response!.Status);Assert.False((await response.AllHeadersAsync()).ContainsKey("x-robots-tag"));
                Assert.Equal(app+path,await page.Locator("link[rel=canonical]").GetAttributeAsync("href"));Assert.NotEmpty(await page.Locator("meta[name=description]").GetAttributeAsync("content")??"");
                Assert.Equal(1,await page.GetByRole(AriaRole.Navigation,new(){Name="More from this ecosystem"}).CountAsync());
                Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth<=innerWidth+1"));
            }
            await page.GotoAsync(app+"/Demo");await page.GetByRole(AriaRole.Button,new(){Name="Simulate reservation request"}).ClickAsync();await Assertions.Expect(page.GetByRole(AriaRole.Heading,new(){Name="Request received — Pending"})).ToBeVisibleAsync();await page.GetByRole(AriaRole.Button,new(){Name="Simulate owner confirmation"}).ClickAsync();await Assertions.Expect(page.GetByRole(AriaRole.Heading,new(){Name="Confirmed — sample dates unavailable"})).ToBeVisibleAsync();
            await using(var check=fixture.Db()){Assert.False(await check.Requests.AnyAsync());Assert.False(await check.Reservations.AnyAsync());}
            Assert.Empty(external);var sitemap=await http.GetStringAsync(app+"/sitemap.xml");Assert.DoesNotContain("Owner",sitemap);Assert.DoesNotContain("GuestStatus",sitemap);Assert.DoesNotContain("request/",sitemap);Assert.DoesNotContain("Privacy",sitemap);
            foreach(var path in new[]{"/Privacy","/Terms","/Contact","/GuestStatus"}){var r=await http.GetAsync(app+path);Assert.Contains("noindex",r.Headers.GetValues("X-Robots-Tag").Single());}
            var invalidGo=await http.GetAsync(app+"/go/https-evil.test");Assert.Equal(404,(int)invalidGo.StatusCode);
            await using var owner=await browser.NewContextAsync(new(){IgnoreHTTPSErrors=true,ViewportSize=new(){Width=390,Height=844}});var op=await owner.NewPageAsync();await op.GotoAsync(app+"/SignIn");await op.GetByRole(AriaRole.Button,new(){Name="Use Alex"}).ClickAsync();
            await op.GetByRole(AriaRole.Link,new(){Name="Plan and usage"}).ClickAsync();await op.GetByLabel("Remove widget branding").CheckAsync();await op.GetByLabel("I understand that selecting Free").CheckAsync();await op.GetByRole(AriaRole.Button,new(){Name="Save plan preferences"}).ClickAsync();
            Assert.DoesNotContain("Booking requests by",await http.GetStringAsync(app+"/w/"+propertyId));Assert.DoesNotContain("More from this ecosystem",await op.ContentAsync());
            await op.GetByLabel("Plan preference").SelectOptionAsync("Free");await op.GetByLabel("I understand that selecting Free").CheckAsync();await op.GetByRole(AriaRole.Button,new(){Name="Save plan preferences"}).ClickAsync();Assert.Contains("Booking requests by",await http.GetStringAsync(app+"/w/"+propertyId));
            await op.GotoAsync(app+"/Owner/Feedback?kind=Custom");await op.GetByLabel("Task, problem or requirements").FillAsync("I need a different calendar view.");
            var forged=await op.EvaluateAsync<int>("async()=>{const f=document.querySelector('main form');const d=new FormData(f);d.delete('__RequestVerificationToken');return(await fetch(f.action,{method:'POST',body:d})).status;}");Assert.Equal(400,forged);
            var key=await op.Locator("input[name=Key]").InputValueAsync();
            var saved=await op.EvaluateAsync<int>("async()=>{const f=document.querySelector('main form');const d=new FormData(f);d.set('Acknowledged','true');d.set('Comment','FORGED_NOTE');d.set('Response','FORGED_RESPONSE');d.set('Status','Closed');d.set('SubmitterUserId','00000000-0000-0000-0000-000000000000');return(await fetch(f.action,{method:'POST',body:d})).status;}");Assert.Equal(200,saved);
            await using(var check=fixture.Db())
            {var row=await check.Feedback.SingleAsync(x=>x.SubmitterUserId==ownerId);Assert.Equal("New",row.Status);Assert.Equal("",row.Comment);Assert.Null(row.Response);row.Comment="DISTINCT_PRIVATE_COMMENT";row.Response="DISTINCT_PRIVATE_RESPONSE";await check.SaveChangesAsync();}
            await op.GetByLabel("I voluntarily submit").CheckAsync();await op.GetByRole(AriaRole.Button,new(){Name="Send privately"}).ClickAsync();await Assertions.Expect(op.GetByRole(AriaRole.Heading,new(){Name="Submission received"})).ToBeVisibleAsync();var html=await op.ContentAsync();Assert.DoesNotContain("DISTINCT_PRIVATE",html);Assert.DoesNotContain("FORGED",html);
            await using(var check=fixture.Db()){Assert.Equal(1,await check.Feedback.CountAsync(x=>x.SubmitterUserId==ownerId));Assert.Equal("Free",(await check.Properties.SingleAsync(x=>x.Id==propertyId)).Plan);}
            var forbidden=await guest.NewPageAsync();await forbidden.GotoAsync(app+"/Owner/Feedback");Assert.Contains("SignIn",forbidden.Url);
            await op.GotoAsync(app+"/Pricing");Directory.CreateDirectory(Path.Combine(root,"artifacts/qa"));await op.ScreenshotAsync(new(){Path=Path.Combine(root,"artifacts/qa/pricing-mobile.png"),FullPage=true});
            await op.SetViewportSizeAsync(320,800);await op.EvaluateAsync("document.body.style.fontSize='32px'");Assert.True(await op.EvaluateAsync<bool>("document.documentElement.scrollWidth<=innerWidth+1"));
            await op.ScreenshotAsync(new(){Path=Path.Combine(root,"artifacts/qa/pricing-enlarged-text.png"),FullPage=true});
        }
        finally{if(!server.HasExited)server.Kill(true);await server.WaitForExitAsync();}
    }
}

