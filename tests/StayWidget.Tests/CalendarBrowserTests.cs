using System.Diagnostics;
using Microsoft.Playwright;
using Xunit;
namespace StayWidget.Tests;

public sealed class CalendarBrowserTests(TestDatabase fixture):IClassFixture<TestDatabase>
{
    [Fact] public async Task CalendarAlternatesRangeEndpointsPreservesMonthsAndAllowsDepartureOnBlockedNight()
    {
        var root=Environment.GetEnvironmentVariable("STAYWIDGET_ROOT")!;const string app="https://localhost:6274";
        var start=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Fantoolie/dotnet/dotnet.exe"),$"\"{root}/src/StayWidget.Web/bin/Release/net10.0/StayWidget.Web.dll\"")
        {WorkingDirectory=Path.Combine(root,"src/StayWidget.Web"),UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        start.Environment["ASPNETCORE_ENVIRONMENT"]="Development";start.Environment["ASPNETCORE_URLS"]=app;start.Environment["StayWidget__PublicOrigin"]=app;start.Environment["ConnectionStrings__StayWidget"]=fixture.Connection;start.Environment["Authentication__Enabled"]="false";start.Environment["Email__Mode"]="Disabled";
        using var server=Process.Start(start)!;server.BeginOutputReadLine();server.BeginErrorReadLine();
        try
        {
            using var http=new HttpClient(new HttpClientHandler{ServerCertificateCustomValidationCallback=(_,_,_,_)=>true});bool ready=false;
            for(int i=0;i<60;i++){try{if((await http.GetAsync(app)).IsSuccessStatusCode){ready=true;break;}}catch(HttpRequestException){}await Task.Delay(200);}Assert.True(ready);
            using var pw=await Playwright.CreateAsync();await using var browser=await pw.Chromium.LaunchAsync(new(){Headless=true});
            await using var context=await browser.NewContextAsync(new(){IgnoreHTTPSErrors=true,ViewportSize=new(){Width=390,Height=844}});var page=await context.NewPageAsync();await page.GotoAsync(app+"/Demo");
            var demo=page.Locator("[data-staywidget=demo] iframe").ContentFrame;await demo.GetByRole(AriaRole.Table).WaitForAsync();
            async Task Go(DateOnly day)
            {
                var target=new DateOnly(day.Year,day.Month,1);var current=DateOnly.Parse((await demo.Locator("[data-night]").First.GetAttributeAsync("data-night"))!);
                while(current!=target)
                {
                    var next=current<target;current=current.AddMonths(next?1:-1);await demo.GetByRole(AriaRole.Button,new(){Name=next?"Next month":"Previous month",Exact=true}).ClickAsync();
                    await Assertions.Expect(demo.Locator("[data-night]").First).ToHaveAttributeAsync("data-night",current.ToString("yyyy-MM-dd"));
                }
            }
            ILocator Cell(DateOnly day)=>demo.Locator($"[data-night='{day:yyyy-MM-dd}']");
            var today=DateOnly.FromDateTime(DateTime.UtcNow);var from=today.AddDays(1);var to=from.AddDays(3);
            await Go(from);await Cell(from).Locator("button").ClickAsync();Assert.Equal(from.ToString("yyyy-MM-dd"),await demo.Locator("#Arrival").InputValueAsync());Assert.Empty(await demo.Locator("#Departure").InputValueAsync());
            // A same-day second click is rejected without consuming the check-out step.
            await Cell(from).Locator("button").ClickAsync();await Assertions.Expect(demo.Locator("[data-calendar-status]")).ToContainTextAsync("Check-out must be later");Assert.Empty(await demo.Locator("#Departure").InputValueAsync());
            await Go(to);await Cell(to).Locator("button").FocusAsync();await Cell(to).Locator("button").PressAsync("Enter");Assert.Equal(to.ToString("yyyy-MM-dd"),await demo.Locator("#Departure").InputValueAsync());
            await Assertions.Expect(Cell(to)).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("range-end"));await Assertions.Expect(Cell(to).Locator("small").First).ToHaveTextAsync("Out");Assert.False(await Cell(to).EvaluateAsync<bool>("el=>el.classList.contains('selected-night')"));
            await Go(from);await Assertions.Expect(Cell(from)).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("range-start"));await Assertions.Expect(Cell(from)).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("selected-night"));
            // Third/fourth clicks select a new stay ending on the first blocked night (departure excluded).
            var nextFrom=today.AddDays(13);var nextTo=today.AddDays(14);await Go(nextFrom);await Cell(nextFrom).Locator("button").ClickAsync();Assert.Empty(await demo.Locator("#Departure").InputValueAsync());
            await Go(nextTo);await Cell(nextTo).Locator("button").ClickAsync();Assert.Equal(nextFrom.ToString("yyyy-MM-dd"),await demo.Locator("#Arrival").InputValueAsync());Assert.Equal(nextTo.ToString("yyyy-MM-dd"),await demo.Locator("#Departure").InputValueAsync());
            await Assertions.Expect(Cell(nextTo)).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("unavailable-night.*range-end"));Assert.False(await Cell(nextTo).EvaluateAsync<bool>("el=>el.classList.contains('selected-night')"));
            // Manual fields update the highlight and restart the next calendar click at check-in.
            await demo.Locator("#Arrival").FillAsync(from.ToString("yyyy-MM-dd"));await demo.Locator("#Departure").FillAsync(to.ToString("yyyy-MM-dd"));await demo.Locator("#Guests").FocusAsync();await Go(to);
            await Assertions.Expect(Cell(to)).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("range-end"));await Cell(to).Locator("button").ClickAsync();Assert.Equal(to.ToString("yyyy-MM-dd"),await demo.Locator("#Arrival").InputValueAsync());Assert.Empty(await demo.Locator("#Departure").InputValueAsync());
            // Cross-month selection persists on navigation; returning to its first month restores the highlight.
            var monthEnd=new DateOnly(today.Year,today.Month,DateTime.DaysInMonth(today.Year,today.Month)).AddMonths(1);monthEnd=new(monthEnd.Year,monthEnd.Month,DateTime.DaysInMonth(monthEnd.Year,monthEnd.Month));var across=monthEnd.AddDays(2);
            await demo.Locator("#Arrival").FillAsync(monthEnd.AddDays(-1).ToString("yyyy-MM-dd"));await demo.Locator("#Departure").FillAsync(across.ToString("yyyy-MM-dd"));await demo.Locator("#Guests").FocusAsync();await Go(monthEnd);
            await Cell(monthEnd).Locator("button").ClickAsync();await Go(across);await Cell(across).Locator("button").ClickAsync();Assert.Equal(monthEnd.ToString("yyyy-MM-dd"),await demo.Locator("#Arrival").InputValueAsync());Assert.Equal(across.ToString("yyyy-MM-dd"),await demo.Locator("#Departure").InputValueAsync());
            await Assertions.Expect(Cell(across.AddDays(-1))).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("selected-night"));await Go(monthEnd);await Assertions.Expect(Cell(monthEnd)).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("range-start"));
            await demo.Locator("#Arrival").FillAsync(monthEnd.AddDays(-6).ToString("yyyy-MM-dd"));await demo.Locator("#Departure").FillAsync(monthEnd.AddDays(-3).ToString("yyyy-MM-dd"));await demo.Locator("#Guests").FocusAsync();
            Assert.True(await demo.Locator("body").EvaluateAsync<bool>("()=>document.documentElement.scrollWidth<=innerWidth+1"));Directory.CreateDirectory(Path.Combine(root,"artifacts/qa"));await page.ScreenshotAsync(new(){Path=Path.Combine(root,"artifacts/qa/calendar-range-mobile.png"),FullPage=true});
        }
        finally {if(!server.HasExited){server.Kill(true);await server.WaitForExitAsync();}}
    }
}
