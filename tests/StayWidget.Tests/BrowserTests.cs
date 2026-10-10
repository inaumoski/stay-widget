using System.Diagnostics;
using Microsoft.Playwright;
using Xunit;
namespace StayWidget.Tests;

public sealed class BrowserTests(TestDatabase db):IClassFixture<TestDatabase>
{
    [Fact] public async Task OwnerGuestConfirmationFlowWorksOnMobileWithoutGuestCookies()
    {
        var root=Environment.GetEnvironmentVariable("STAYWIDGET_ROOT")!;
        var start=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Fantoolie/dotnet/dotnet.exe"),$"\"{root}/src/StayWidget.Web/bin/Release/net10.0/StayWidget.Web.dll\"") {WorkingDirectory=Path.Combine(root,"src/StayWidget.Web"),UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        start.Environment["ASPNETCORE_ENVIRONMENT"]="Development"; start.Environment["ASPNETCORE_URLS"]="https://localhost:6269";
        start.Environment["ConnectionStrings__StayWidget"]=db.Connection;start.Environment["Authentication__Enabled"]="false";start.Environment["StayWidget__DevelopmentLogin"]="true";
        using var server=Process.Start(start)!;server.BeginOutputReadLine();server.BeginErrorReadLine();
        try
        {
            using var client=new HttpClient(new HttpClientHandler{ServerCertificateCustomValidationCallback=(_,_,_,_)=>true});
            bool ready=false;for(var i=0;i<60;i++){try{if((await client.GetAsync("https://localhost:6269")).IsSuccessStatusCode){ready=true;break;}}catch(HttpRequestException){}await Task.Delay(200);}Assert.True(ready,"Preview server did not start.");
            using var pw=await Playwright.CreateAsync();
            await using var browser=await pw.Chromium.LaunchAsync(new(){Headless=true});
            await using var owner=await browser.NewContextAsync(new(){IgnoreHTTPSErrors=true});var page=await owner.NewPageAsync();
            await page.GotoAsync("https://localhost:6269/SignIn");await page.GetByRole(AriaRole.Button,new(){Name="Use Alex"}).ClickAsync();
            await page.GetByLabel("Property name",new(){Exact=true}).FillAsync("Browser Test Cottage");await page.GetByLabel("Public contact").FillAsync("+389 70 111 111");
            await page.GetByLabel("Booking and cancellation policies").FillAsync("Contact the owner to agree price and cancellation terms before confirming.");await page.GetByLabel("Unit name",new(){Exact=true}).FillAsync("Garden Cottage");await page.Locator("input[name=understand]").CheckAsync();await page.GetByRole(AriaRole.Button,new(){Name="Create property and unit"}).ClickAsync();
            await page.GetByRole(AriaRole.Link,new(){Name="Open guest request page"}).WaitForAsync();var guestPath=await page.GetByRole(AriaRole.Link,new(){Name="Open guest request page"}).GetAttributeAsync("href");
            await using var guest=await browser.NewContextAsync(new(){IgnoreHTTPSErrors=true,ViewportSize=new(){Width=390,Height=844}});var gp=await guest.NewPageAsync();
            await gp.GotoAsync("https://localhost:6269"+guestPath);await gp.GetByLabel("Name",new(){Exact=true}).FillAsync("Browser Guest");await gp.GetByLabel("Phone, including country code").FillAsync("+389 70 222 222");await gp.Locator("input[type=checkbox][name=Agree]").CheckAsync();await gp.GetByRole(AriaRole.Button,new(){Name="Send reservation request"}).ClickAsync();
            await Assertions.Expect(gp.GetByRole(AriaRole.Heading,new(){Name="Request received — awaiting approval"})).ToBeVisibleAsync();
            Assert.Empty(await guest.CookiesAsync());Assert.DoesNotContain("222 222",await gp.Locator("#status").InnerTextAsync());
            await page.ReloadAsync();
            var malformed=await page.EvaluateAsync<int>("async()=>{const f=document.querySelector('form[action*=Confirm]');const data=new FormData(f);data.set('amount','not-a-price');data.set('agreed','true');return(await fetch(f.action,{method:'POST',body:data})).status;}");Assert.Equal(409,malformed);
            var forged=await page.EvaluateAsync<int>("async()=>{const f=document.querySelector('form[action*=Confirm]');const data=new FormData(f);data.delete('__RequestVerificationToken');data.set('amount','120');data.set('agreed','true');return(await fetch(f.action,{method:'POST',body:data})).status;}");Assert.Equal(400,forged);
            await page.GetByLabel("Externally agreed total (EUR)").FillAsync("120");await page.Locator("input[name=agreed]").CheckAsync();await page.GetByRole(AriaRole.Button,new(){Name="Confirm reservation"}).ClickAsync();await Assertions.Expect(page.Locator(".status")).ToHaveTextAsync("Accepted");
            await gp.ReloadAsync();await Assertions.Expect(gp.GetByRole(AriaRole.Heading,new(){Name="Reservation confirmed"})).ToBeVisibleAsync();
            var fresh=await guest.NewPageAsync();await fresh.GotoAsync("https://localhost:6269"+guestPath);await fresh.GetByRole(AriaRole.Button,new(){Name="Check availability"}).ClickAsync();await Assertions.Expect(fresh.GetByRole(AriaRole.Status)).ToContainTextAsync("unavailable");
            Assert.False(await gp.EvaluateAsync<bool>("document.documentElement.scrollWidth > innerWidth"));
            await gp.SetViewportSizeAsync(320,700);await gp.EvaluateAsync("document.body.style.fontSize='34px';document.querySelector('#status h2').textContent='A'.repeat(120)");Assert.False(await gp.EvaluateAsync<bool>("document.documentElement.scrollWidth > innerWidth"));await gp.EvaluateAsync("document.body.style.fontSize=''");await gp.SetViewportSizeAsync(390,844);await gp.ReloadAsync();await Assertions.Expect(gp.GetByRole(AriaRole.Heading,new(){Name="Reservation confirmed"})).ToBeVisibleAsync();
            var response=await client.GetAsync("https://localhost:6269/GuestStatus");Assert.Contains("none",response.Headers.GetValues("Content-Security-Policy").Single());Assert.Contains("no-store",response.Headers.CacheControl!.ToString());
            var outDir=Path.Combine(root,"artifacts/qa");Directory.CreateDirectory(outDir);await page.ScreenshotAsync(new(){Path=Path.Combine(outDir,"owner-confirmed.png"),FullPage=true});await gp.ScreenshotAsync(new(){Path=Path.Combine(outDir,"guest-mobile-confirmed.png"),FullPage=true});
            await using var other=await browser.NewContextAsync(new(){IgnoreHTTPSErrors=true});var sp=await other.NewPageAsync();await sp.GotoAsync("https://localhost:6269/SignIn");await sp.GetByRole(AriaRole.Button,new(){Name="Use Sam"}).ClickAsync();Assert.DoesNotContain("Browser Guest",await sp.Locator("main").InnerTextAsync());
        }
        finally{if(!server.HasExited){server.Kill(true);await server.WaitForExitAsync();}}
    }
}
