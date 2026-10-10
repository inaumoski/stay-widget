using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using StayWidget.Web;
using Xunit;
namespace StayWidget.Tests;

public sealed class EmbedBrowserTests(TestDatabase fixture) : IClassFixture<TestDatabase>
{
    [Fact] public async Task CrossOriginEmbedAndPrivateActionsWorkWithoutStorageAndRejectUnsafeMessages()
    {
        const string app="https://localhost:6271", host="http://127.0.0.1:6280";
        var root=Environment.GetEnvironmentVariable("STAYWIDGET_ROOT")!;Guid ownerId,propertyId;
        await using(var db=fixture.Db())
        {
            var s=fixture.Service(db);ownerId=await s.User("urn:staywidget:development","Alex","Alex");
            propertyId=(await s.Setup(ownerId,"Embed Cottage","+389 70 333 333","Europe/Skopje","Call the property to agree price and cancellation before confirmation.","Garden Cottage",2)).Id;
        }
        string Html(bool direct)=>"<!doctype html><html><head><meta name=\"viewport\" content=\"width=device-width\"></head><body><h1>Independent property website</h1>"+
            (direct?$"<iframe src=\"{app}/w/{propertyId}\" title=\"Direct reservation requests\" width=\"100%\" height=\"860\"></iframe><a href=\"{app}/request/{propertyId}\">Hosted fallback</a>":
            $"<script>window.seen=[];addEventListener('message',e=>seen.push(e.data));</script><div data-staywidget=\"{propertyId}\"><a href=\"{app}/request/{propertyId}\">Hosted fallback one</a></div><div data-staywidget=\"{propertyId}\"><a href=\"{app}/request/{propertyId}\">Hosted fallback two</a></div><script src=\"{app}/embed/v1/loader.js\" defer></script><script src=\"{app}/embed/v1/loader.js\" defer></script>")+"</body></html>";
        await using var approved=new TestHost(6280,Html);await using var unapproved=new TestHost(6281,Html);
        var start=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Fantoolie/dotnet/dotnet.exe"),$"\"{root}/src/StayWidget.Web/bin/Release/net10.0/StayWidget.Web.dll\"")
            {WorkingDirectory=Path.Combine(root,"src/StayWidget.Web"),UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        start.Environment["ASPNETCORE_ENVIRONMENT"]="Development";start.Environment["ASPNETCORE_URLS"]=app;start.Environment["StayWidget__PublicOrigin"]=app;
        start.Environment["ConnectionStrings__StayWidget"]=fixture.Connection;start.Environment["Authentication__Enabled"]="false";start.Environment["StayWidget__DevelopmentLogin"]="true";
        start.Environment["Email__Mode"]="Disabled";
        using var server=Process.Start(start)!;server.BeginOutputReadLine();server.BeginErrorReadLine();
        try
        {
            using var http=new HttpClient(new HttpClientHandler{ServerCertificateCustomValidationCallback=(_,_,_,_)=>true});
            bool ready=false;for(int i=0;i<60;i++){try{if((await http.GetAsync(app)).IsSuccessStatusCode){ready=true;break;}}catch(HttpRequestException){}await Task.Delay(200);}Assert.True(ready);
            using var pw=await Playwright.CreateAsync();await using var browser=await pw.Chromium.LaunchAsync(new(){Headless=true,Args=["--test-third-party-cookie-phaseout"]});
            await using var owner=await browser.NewContextAsync(new(){IgnoreHTTPSErrors=true});var op=await owner.NewPageAsync();
            await op.GotoAsync(app+"/SignIn");await op.GetByRole(AriaRole.Button,new(){Name="Use Alex"}).ClickAsync();await op.GetByRole(AriaRole.Link,new(){Name="Install widget"}).ClickAsync();
            await op.GetByLabel("Approved website origins").FillAsync(host);await op.GetByLabel("I am authorized to install").CheckAsync();await op.GetByRole(AriaRole.Button,new(){Name="Save installation settings"}).ClickAsync();
            Assert.Contains("/embed/v1/loader.js",await op.Locator("#loader-snippet").InputValueAsync());Assert.DoesNotContain("Token",await op.Locator("#loader-snippet").InputValueAsync());
            var widget=await http.GetAsync(app+"/w/"+propertyId);Assert.False(widget.Headers.Contains("X-Frame-Options"));Assert.Contains(host,widget.Headers.GetValues("Content-Security-Policy").Single());Assert.DoesNotContain("indexifembedded",widget.Headers.GetValues("X-Robots-Tag").Single());
            foreach(var path in new[]{"/Owner","/GuestStatus","/request/"+propertyId}){var response=await http.GetAsync(app+path);Assert.Contains("'none'",response.Headers.GetValues("Content-Security-Policy").Single());Assert.Equal("DENY",response.Headers.GetValues("X-Frame-Options").Single());}
            var loader=await http.GetAsync(app+"/embed/v1/loader.js");Assert.Contains("max-age=300",loader.Headers.CacheControl!.ToString());Assert.NotNull(loader.Headers.ETag);
            await using var guest=await browser.NewContextAsync(new(){IgnoreHTTPSErrors=true,ViewportSize=new(){Width=390,Height=844}});
            await guest.AddInitScriptAsync("for(const k of ['localStorage','sessionStorage'])Object.defineProperty(window,k,{get(){throw new Error('Storage unavailable in this test');}});");
            var page=await guest.NewPageAsync();await page.GotoAsync(host);await Assertions.Expect(page.Locator("iframe")).ToHaveCountAsync(2);
            await page.WaitForFunctionAsync("[...document.querySelectorAll('iframe')].every(f=>f.height!=='860')");
            var first=page.Locator("iframe").Nth(0).ContentFrame;var second=page.Locator("iframe").Nth(1).ContentFrame;
            await Assertions.Expect(first.GetByRole(AriaRole.Heading,new(){Name="Embed Cottage",Exact=true})).ToBeVisibleAsync();
            Assert.False(await first.Locator("body").EvaluateAsync<bool>("()=>document.documentElement.scrollWidth>innerWidth"));
            Assert.Equal(2,await page.GetByRole(AriaRole.Link,new(){Name="Hosted fallback",Exact=false}).CountAsync());
            var heights=await page.EvaluateAsync<string[]>("[...document.querySelectorAll('iframe')].map(f=>f.height)");
            await first.Locator("body").EvaluateAsync("b=>{const x=new URL(location.href).searchParams.get('InstanceId');for(const h of [NaN,Infinity,0,1601,777.5])parent.postMessage({v:1,type:'staywidget:resize',instance:x,height:h},'http://127.0.0.1:6280');parent.postMessage({v:1,type:'staywidget:resize',instance:x,height:777,private:'unexpected'},'http://127.0.0.1:6280');}");
            var instance=await page.Locator("iframe").Nth(0).GetAttributeAsync("data-staywidget-instance");
            await first.Locator("body").EvaluateAsync("(b,id)=>{dispatchEvent(new MessageEvent('message',{origin:'https://unapproved.example',source:parent,data:{v:1,type:'staywidget:init',instance:id}}));dispatchEvent(new MessageEvent('message',{origin:'http://127.0.0.1:6280',source:window,data:{v:1,type:'staywidget:init',instance:id}}));}",instance);
            await second.Locator("body").EvaluateAsync("(b,id)=>parent.postMessage({v:1,type:'staywidget:resize',instance:id,height:777},'http://127.0.0.1:6280')",instance);
            await page.EvaluateAsync("id=>postMessage({v:1,type:'staywidget:resize',instance:id,height:777},location.origin)",instance);
            await Task.Delay(150);Assert.Equal(heights,await page.EvaluateAsync<string[]>("[...document.querySelectorAll('iframe')].map(f=>f.height)"));
            await first.GetByLabel("Name",new(){Exact=true}).FillAsync("Embed Guest");await first.GetByLabel("Phone, including country code").FillAsync("+389 70 444 444");await first.GetByLabel("Email for confirmation").FillAsync("guest@example.invalid");await first.Locator("input[type=checkbox][name=Agree]").CheckAsync();
            await first.Locator("input[name=Website]").EvaluateAsync("el=>el.value='bot.example'");await first.GetByRole(AriaRole.Button,new(){Name="Send reservation request"}).ClickAsync();await Assertions.Expect(first.GetByRole(AriaRole.Alert)).ToContainTextAsync("could not be accepted");
            await using(var db=fixture.Db())Assert.False(await db.Requests.AnyAsync(x=>x.PropertyId==propertyId));
            Assert.Equal("Embed Guest",await first.GetByLabel("Name",new(){Exact=true}).InputValueAsync());await first.Locator("input[name=Website]").EvaluateAsync("el=>el.value=''");
            await first.GetByRole(AriaRole.Button,new(){Name="Send reservation request"}).ClickAsync();await Assertions.Expect(first.GetByRole(AriaRole.Heading,new(){Name="Request received — awaiting approval"})).ToBeVisibleAsync();
            var receiptHeight=await first.Locator("body").EvaluateAsync<int>("b=>Math.max(320,Math.min(1600,Math.ceil(b.getBoundingClientRect().height)))");
            await Assertions.Expect(page.Locator("iframe").Nth(0)).ToHaveAttributeAsync("height",receiptHeight.ToString());
            var privateLink=await first.GetByRole(AriaRole.Link,new(){Name="Open private status and actions"}).GetAttributeAsync("href");
            var secrets=new Uri(privateLink!).Fragment[1..].Split('.');
            Assert.DoesNotContain(secrets[0],await page.EvaluateAsync<string>("JSON.stringify(seen)"));Assert.DoesNotContain(secrets[1],await page.EvaluateAsync<string>("JSON.stringify(seen)"));
            var popupTask=page.WaitForPopupAsync();await first.GetByRole(AriaRole.Link,new(){Name="Open private status and actions"}).ClickAsync();var status=await popupTask;
            await Assertions.Expect(status.GetByRole(AriaRole.Heading,new(){Name="Request received — awaiting approval"})).ToBeVisibleAsync();
            Assert.Empty(await guest.CookiesAsync());var before=await guest.APIRequest.GetAsync(app+"/api/guest/withdraw");Assert.Equal(405,before.Status);
            var wrongScope=await guest.APIRequest.PostAsync(app+"/api/guest/withdraw",new(){DataObject=new{Confirmed=true},Headers=new Dictionary<string,string>{{"Authorization","Bearer "+secrets[0]}}});Assert.Equal(404,wrongScope.Status);
            var crossSite=await guest.APIRequest.PostAsync(app+"/api/guest/withdraw",new(){DataObject=new{Confirmed=true},Headers=new Dictionary<string,string>{{"Authorization","Bearer "+secrets[1]},{"Origin","https://unapproved.example"}}});Assert.Equal(400,crossSite.Status);
            await op.GotoAsync(app+"/Owner");await op.Locator("input[name=amount]").FillAsync("120");await op.Locator("input[name=agreed]").CheckAsync();await op.GetByRole(AriaRole.Button,new(){Name="Confirm reservation",Exact=true}).ClickAsync();
            await using(var db=fixture.Db()){Assert.Equal("Queued",(await db.ConfirmationEmails.SingleAsync(x=>x.PropertyId==propertyId)).State);}
            var bookedNight=await second.GetByLabel("Check-in",new(){Exact=true}).InputValueAsync();await second.Locator("body").EvaluateAsync("()=>document.dispatchEvent(new Event('visibilitychange'))");
            await Assertions.Expect(second.GetByLabel(bookedNight+": Unavailable night",new(){Exact=true})).ToBeVisibleAsync();
            var configuration=await http.GetStringAsync(app+"/api/widget/"+propertyId+"/configuration");Assert.DoesNotContain("ownerId",configuration);Assert.DoesNotContain("embedOrigins",configuration);Assert.DoesNotContain("444 444",configuration);
            await using(var db=fixture.Db()){var r=await db.Requests.SingleAsync(x=>x.PropertyId==propertyId);var availability=await http.PostAsJsonAsync(app+"/api/widget/"+propertyId+"/availability",new { r.Arrival,r.Departure,r.Guests });var body=await availability.Content.ReadAsStringAsync();Assert.Contains("\"available\":false",body);Assert.DoesNotContain("Embed Guest",body);Assert.DoesNotContain("444 444",body);Assert.False(availability.Headers.Contains("Access-Control-Allow-Origin"));}
            await status.ReloadAsync();await Assertions.Expect(status.GetByRole(AriaRole.Heading,new(){Name="Reservation confirmed"})).ToBeVisibleAsync();
            await status.GetByLabel("I understand that this only asks").CheckAsync();await status.GetByRole(AriaRole.Button,new(){Name="Request cancellation",Exact=true}).ClickAsync();
            await Assertions.Expect(status.GetByText("Cancellation requested — awaiting owner review.",new(){Exact=false})).ToBeVisibleAsync();
            await using(var db=fixture.Db()){Assert.True((await db.Reservations.SingleAsync(x=>x.PropertyId==propertyId)).CancellationRequested);Assert.Single(await db.Allocations.Where(x=>x.PropertyId==propertyId&&x.Active).ToListAsync());}
            var view=await guest.NewPageAsync();await view.GotoAsync(app+"/GuestStatus#"+secrets[0]);await Assertions.Expect(view.GetByRole(AriaRole.Heading,new(){Name="Reservation confirmed"})).ToBeVisibleAsync();Assert.Equal(0,await view.Locator("#guest-actions form").CountAsync());
            var outDir=Path.Combine(root,"artifacts/qa");Directory.CreateDirectory(outDir);await page.ScreenshotAsync(new(){Path=Path.Combine(outDir,"embedded-mobile-receipt.png"),FullPage=false});
            var blocked=await guest.NewPageAsync();var denied=new TaskCompletionSource<bool>();blocked.Console+=(_,m)=>{if(m.Text.Contains("frame-ancestors",StringComparison.OrdinalIgnoreCase))denied.TrySetResult(true);};
            await blocked.GotoAsync("http://127.0.0.1:6281");Assert.True(await Task.WhenAny(denied.Task,Task.Delay(5000))==denied.Task,"Unapproved host should fail CSP framing.");
            // Direct iframe remains usable without JavaScript and storage: a server-rendered receipt follows POST.
            await using var noJs=await browser.NewContextAsync(new(){IgnoreHTTPSErrors=true,JavaScriptEnabled=false,ViewportSize=new(){Width=390,Height=844}});var direct=await noJs.NewPageAsync();await direct.GotoAsync(host+"/direct");
            var df=direct.FrameLocator("iframe");await df.GetByLabel("Check-in",new(){Exact=true}).FillAsync(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(20).ToString("yyyy-MM-dd"));await df.GetByLabel("Check-out",new(){Exact=true}).FillAsync(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(22).ToString("yyyy-MM-dd"));
            await df.GetByLabel("Name",new(){Exact=true}).FillAsync("No script guest");await df.GetByLabel("Phone, including country code").FillAsync("+389 70 555 555");await df.GetByLabel("Email for confirmation").FillAsync("guest@example.invalid");await df.Locator("input[type=checkbox][name=Agree]").CheckAsync();await df.GetByRole(AriaRole.Button,new(){Name="Send reservation request"}).ClickAsync();await Assertions.Expect(df.GetByRole(AriaRole.Heading,new(){Name="Request received — awaiting approval"})).ToBeVisibleAsync();Assert.Empty(await noJs.CookiesAsync());
            // Seed this disposable property to its daily safety limit, then verify the real HTML rejection preserves input.
            await using(var db=fixture.Db())
            {
                var unit=await db.Units.SingleAsync(x=>x.PropertyId==propertyId);
                var count=await db.Requests.CountAsync(x=>x.PropertyId==propertyId);
                for(int i=count;i<100;i++)db.Requests.Add(new(){PropertyId=propertyId,UnitId=unit.Id,Arrival=DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30),Departure=DateOnly.FromDateTime(DateTime.UtcNow).AddDays(32),Guests=1,Name="Synthetic safety seed",Phone="+389 70 000 000",Created=DateTimeOffset.UtcNow,Expires=DateTimeOffset.UtcNow.AddHours(48),KeyHash=BookingService.Hash(BookingService.Secret()),TokenHash=BookingService.Hash(BookingService.Secret())});
                await db.SaveChangesAsync();
            }
            var limited=await guest.NewPageAsync();await limited.GotoAsync(app+"/request/"+propertyId);await limited.GetByLabel("Check-in",new(){Exact=true}).FillAsync(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(40).ToString("yyyy-MM-dd"));await limited.GetByLabel("Check-out",new(){Exact=true}).FillAsync(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(42).ToString("yyyy-MM-dd"));
            await limited.GetByLabel("Name",new(){Exact=true}).FillAsync("Retained form input");await limited.GetByLabel("Phone, including country code").FillAsync("+389 70 666 666");await limited.GetByLabel("Email for confirmation").FillAsync("guest@example.invalid");await limited.Locator("input[type=checkbox][name=Agree]").CheckAsync();
            var rejectionTask=limited.WaitForResponseAsync(r=>r.Request.Method=="POST"&&r.Url.Contains("/request/"));await limited.GetByRole(AriaRole.Button,new(){Name="Send reservation request"}).ClickAsync();var rejection=await rejectionTask;Assert.Equal(429,rejection.Status);Assert.NotNull(await rejection.HeaderValueAsync("Retry-After"));
            await Assertions.Expect(limited.GetByRole(AriaRole.Alert)).ToContainTextAsync("No new request was created");Assert.Equal("Retained form input",await limited.GetByLabel("Name",new(){Exact=true}).InputValueAsync());
            await using(var db=fixture.Db())Assert.Equal(100,await db.Requests.CountAsync(x=>x.PropertyId==propertyId));
        }
        finally{if(!server.HasExited){server.Kill(true);await server.WaitForExitAsync();}}
    }
}

internal sealed class TestHost : IAsyncDisposable
{
    private readonly TcpListener listener;private readonly CancellationTokenSource stop=new();private readonly Task running;
    public TestHost(int port,Func<bool,string> html)
    {
        listener=new TcpListener(IPAddress.Loopback,port);listener.Start();
        running=Task.Run(async()=>
        {
            try { while(!stop.IsCancellationRequested)
            {
                using var client=await listener.AcceptTcpClientAsync(stop.Token);await using var stream=client.GetStream();
                using var reader=new StreamReader(stream,Encoding.ASCII,false,1024,true);var first=await reader.ReadLineAsync(stop.Token);
                for(int i=0;i<50;i++){var line=await reader.ReadLineAsync(stop.Token);if(string.IsNullOrEmpty(line))break;}
                var body=Encoding.UTF8.GetBytes(html(first?.Contains(" /direct ")==true));var headers=Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {body.Length}\r\nConnection: close\r\nCache-Control: no-store\r\n\r\n");
                await stream.WriteAsync(headers,stop.Token);await stream.WriteAsync(body,stop.Token);
            }} catch(Exception e) when(e is OperationCanceledException or SocketException or IOException) { }
        });
    }
    public async ValueTask DisposeAsync(){stop.Cancel();listener.Stop();await running;stop.Dispose();}
}
