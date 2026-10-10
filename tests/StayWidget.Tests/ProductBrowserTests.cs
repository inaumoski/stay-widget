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
        start.Environment["Email__Mode"]="Disabled";
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
            await page.GotoAsync(app+"/Demo");
            var embedded=page.Locator("[data-staywidget=demo] iframe");await embedded.WaitForAsync();var demo=embedded.ContentFrame;
            await demo.GetByRole(AriaRole.Table).WaitForAsync();Assert.True(await demo.Locator("body").EvaluateAsync<bool>("()=>document.documentElement.scrollWidth<=innerWidth+1"));
            var originalMonth=await demo.Locator("[data-calendar-title]").InnerTextAsync();await demo.GetByRole(AriaRole.Button,new(){Name="Next month",Exact=true}).ClickAsync();await Assertions.Expect(demo.Locator("[data-calendar-title]")).Not.ToHaveTextAsync(originalMonth);await demo.GetByRole(AriaRole.Table).WaitForAsync();await demo.GetByRole(AriaRole.Button,new(){Name="Previous month",Exact=true}).ClickAsync();await Assertions.Expect(demo.Locator("[data-calendar-title]")).ToHaveTextAsync(originalMonth);await demo.GetByRole(AriaRole.Table).WaitForAsync();
            await page.RouteAsync("**/api/demo/calendar*",r=>r.FulfillAsync(new(){Status=503,ContentType="application/json",Body="{}"}));await demo.GetByRole(AriaRole.Link,new(){Name="Reset widget demo"}).ClickAsync();await Assertions.Expect(demo.Locator("[data-calendar-status]")).ToContainTextAsync("Calendar unavailable");await page.UnrouteAsync("**/api/demo/calendar*");await demo.GetByRole(AriaRole.Button,new(){Name="Retry calendar"}).ClickAsync();await demo.GetByRole(AriaRole.Table).WaitForAsync();
            await page.WaitForFunctionAsync("()=>{const f=document.querySelector('[data-staywidget=demo] iframe');return Number(f.height)===Math.max(320,Math.min(640,Math.ceil(f.contentDocument.body.getBoundingClientRect().height)));}");
            Directory.CreateDirectory(Path.Combine(root,"artifacts/qa"));await page.ScreenshotAsync(new(){Path=Path.Combine(root,"artifacts/qa/availability-calendar-mobile.png"),FullPage=true});
            var firstAvailable=demo.GetByRole(AriaRole.Button,new(){NameRegex=new System.Text.RegularExpressions.Regex("Available night. Choose check-in")}).First;
            var chosenDate=(await firstAvailable.GetAttributeAsync("aria-label"))![..10];await firstAvailable.ClickAsync();Assert.Equal(chosenDate,await demo.GetByLabel("Check-in",new(){Exact=true}).InputValueAsync());
            // Reset restores a deterministic free two-night sample independently of whichever day was clicked.
            await demo.GetByRole(AriaRole.Link,new(){Name="Reset widget demo"}).ClickAsync();
            await demo.GetByRole(AriaRole.Button,new(){Name="Try a reservation request"}).ClickAsync();await Assertions.Expect(demo.GetByRole(AriaRole.Heading,new(){Name="Request received — Pending"})).ToBeVisibleAsync();
            await demo.GetByRole(AriaRole.Button,new(){Name="Simulate owner confirmation"}).ClickAsync();await Assertions.Expect(demo.GetByRole(AriaRole.Heading,new(){Name="Confirmed — demonstration only"})).ToBeVisibleAsync();
            Assert.True(int.Parse((await embedded.GetAttributeAsync("height"))!) is >=320 and <=640);Assert.DoesNotContain("More from this ecosystem",await demo.Locator("body").InnerTextAsync());
            var demoResponse=await http.GetAsync(app+"/demo-widget");Assert.Contains("'self'",demoResponse.Headers.GetValues("Content-Security-Policy").Single());Assert.Contains("noindex",demoResponse.Headers.GetValues("X-Robots-Tag").Single());Assert.False(demoResponse.Headers.Contains("Set-Cookie"));
            Directory.CreateDirectory(Path.Combine(root,"artifacts/qa"));await page.ScreenshotAsync(new(){Path=Path.Combine(root,"artifacts/qa/demo-embed-mobile.png"),FullPage=true});
            await page.Locator("summary").ClickAsync();Assert.Contains("data-staywidget=\"demo\"",await page.Locator("#demo-snippet").InputValueAsync());
            await page.GetByRole(AriaRole.Button,new(){Name="Simulate reservation request"}).ClickAsync();await Assertions.Expect(page.GetByRole(AriaRole.Heading,new(){Name="Request received — Pending"})).ToBeVisibleAsync();await page.GetByRole(AriaRole.Button,new(){Name="Simulate owner confirmation"}).ClickAsync();await Assertions.Expect(page.GetByRole(AriaRole.Heading,new(){Name="Confirmed — sample dates unavailable"})).ToBeVisibleAsync();
            await using(var check=fixture.Db()){Assert.False(await check.Requests.AnyAsync());Assert.False(await check.Reservations.AnyAsync());}
            Assert.Empty(external);var sitemap=await http.GetStringAsync(app+"/sitemap.xml");Assert.DoesNotContain("Owner",sitemap);Assert.DoesNotContain("GuestStatus",sitemap);Assert.DoesNotContain("request/",sitemap);Assert.DoesNotContain("Privacy",sitemap);
            foreach(var path in new[]{"/Privacy","/Terms","/Contact","/GuestStatus"}){var r=await http.GetAsync(app+path);Assert.Contains("noindex",r.Headers.GetValues("X-Robots-Tag").Single());}
            var invalidGo=await http.GetAsync(app+"/go/https-evil.test");Assert.Equal(404,(int)invalidGo.StatusCode);
            await using var owner=await browser.NewContextAsync(new(){IgnoreHTTPSErrors=true,ViewportSize=new(){Width=390,Height=844}});var op=await owner.NewPageAsync();await op.GotoAsync(app+"/SignIn");await op.GetByRole(AriaRole.Button,new(){Name="Use Alex"}).ClickAsync();
            await op.GetByRole(AriaRole.Link,new(){Name="Plan and usage"}).ClickAsync();await op.GetByLabel("Remove widget branding").CheckAsync();await op.GetByLabel("I understand that selecting Free").CheckAsync();await op.GetByRole(AriaRole.Button,new(){Name="Save plan preferences"}).ClickAsync();
            Assert.DoesNotContain("Booking requests by",await http.GetStringAsync(app+"/w/"+propertyId));Assert.DoesNotContain("More from this ecosystem",await op.ContentAsync());
            await op.GetByLabel("Plan preference").SelectOptionAsync("Free");await op.GetByLabel("I understand that selecting Free").CheckAsync();await op.GetByRole(AriaRole.Button,new(){Name="Save plan preferences"}).ClickAsync();Assert.Contains("Booking requests by",await http.GetStringAsync(app+"/w/"+propertyId));
            await op.GotoAsync(app+"/Owner/Installation");Assert.True(await op.GetByLabel("Show availability calendar in the widget").IsCheckedAsync());await op.GetByLabel("Show availability calendar in the widget").UncheckAsync();await op.GetByRole(AriaRole.Button,new(){Name="Save installation settings"}).ClickAsync();Assert.DoesNotContain("class=\"availability-calendar\"",await http.GetStringAsync(app+"/w/"+propertyId));
            Guid publicUnit;await using(var check=fixture.Db())publicUnit=await check.Units.Where(x=>x.PropertyId==propertyId).Select(x=>x.Id).SingleAsync();var month=AvailabilityCalendar.Month(DateOnly.FromDateTime(DateTime.UtcNow)).ToString("yyyy-MM-dd");Assert.Equal(404,(int)(await http.GetAsync(app+$"/api/widget/{propertyId}/calendar?unitId={publicUnit}&month={month}&guests=2")).StatusCode);
            await op.GetByLabel("Show availability calendar in the widget").CheckAsync();await op.GetByRole(AriaRole.Button,new(){Name="Save installation settings"}).ClickAsync();Assert.Contains("class=\"availability-calendar\"",await http.GetStringAsync(app+"/w/"+propertyId));
            await op.GotoAsync(app+"/Owner/Units?id="+publicUnit);await op.Locator("#season-year").FillAsync("2027");await op.GetByRole(AriaRole.Button,new(){Name="Use November–December"}).ClickAsync();Assert.Equal("2027-11-01",await op.GetByLabel("First bookable night").InputValueAsync());Assert.Equal("2028-01-01",await op.GetByLabel("Season end").InputValueAsync());await op.GetByRole(AriaRole.Button,new(){Name="Use calendar year"}).ClickAsync();Assert.Equal("2027-01-01",await op.GetByLabel("First bookable night").InputValueAsync());
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
