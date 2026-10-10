using System.Diagnostics;
using System.Net;
using Xunit;
namespace StayWidget.Tests;
public sealed class ProductionTests(TestDatabase fixture):IClassFixture<TestDatabase>
{
    [Fact] public async Task ProductionRejectsWrongHostAndDevelopmentLoginAndKeepsReadinessPrivate()
    {
        var root=Environment.GetEnvironmentVariable("STAYWIDGET_ROOT")!;const string app="https://localhost:6275";
        var start=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Fantoolie/dotnet/dotnet.exe"),$"\"{root}/src/StayWidget.Web/bin/Release/net10.0/StayWidget.Web.dll\"")
        {WorkingDirectory=Path.Combine(root,"src/StayWidget.Web"),UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        start.Environment["ASPNETCORE_ENVIRONMENT"]="Production";start.Environment["ASPNETCORE_URLS"]=app;start.Environment["StayWidget__PublicOrigin"]=app;start.Environment["StayWidget__DataRoot"]=Path.Combine(root,".local/production-fixture-keys");
        start.Environment["ConnectionStrings__StayWidget"]=fixture.Connection;start.Environment["Authentication__Enabled"]="true";start.Environment["Authentication__Issuer"]="https://127.0.0.1:1";start.Environment["Authentication__ClientId"]="synthetic-unused";start.Environment["Authentication__ClientSecret"]="synthetic-not-a-provider-credential";
        start.Environment["StayWidget__DevelopmentLogin"]="false";start.Environment["StayWidget__BehindLoopbackProxy"]="true";start.Environment["StayWidget__MaintenanceEnabled"]="false";
        using(var server=Process.Start(start)!)
        {
            server.BeginOutputReadLine();server.BeginErrorReadLine();
            try
            {
                using var http=new HttpClient(new HttpClientHandler{ServerCertificateCustomValidationCallback=(_,_,_,_)=>true,AllowAutoRedirect=false});bool ready=false;
                for(int i=0;i<60;i++){try{if((await http.GetAsync(app+"/health/live")).IsSuccessStatusCode){ready=true;break;}}catch(HttpRequestException){}await Task.Delay(200);}Assert.True(ready);
                var page=await http.GetAsync(app+"/SignIn?failed=true");Assert.DoesNotContain("Use Alex",await page.Content.ReadAsStringAsync());Assert.Equal("DENY",page.Headers.GetValues("X-Frame-Options").Single());
                var wrong=new HttpRequestMessage(HttpMethod.Get,app+"/Pricing");wrong.Headers.Host="evil.example";Assert.Equal(HttpStatusCode.BadRequest,(await http.SendAsync(wrong)).StatusCode);
                var privateReady=new HttpRequestMessage(HttpMethod.Get,app+"/health/ready");privateReady.Headers.Add("X-Forwarded-For","198.51.100.2");privateReady.Headers.Add("X-Forwarded-Proto","https");Assert.Equal(HttpStatusCode.NotFound,(await http.SendAsync(privateReady)).StatusCode);
                Assert.Equal("ready",await http.GetStringAsync(app+"/health/ready"));
                var spoof=new HttpRequestMessage(HttpMethod.Get,app+"/Pricing");spoof.Headers.Add("X-Forwarded-Host","evil.example");var r=await http.SendAsync(spoof);Assert.Equal(HttpStatusCode.OK,r.StatusCode);Assert.Contains(app+"/Pricing",await r.Content.ReadAsStringAsync());
                var owner=await http.GetAsync(app+"/Owner");Assert.Equal(HttpStatusCode.Redirect,owner.StatusCode);Assert.Contains("SignIn",owner.Headers.Location!.ToString());
            }
            finally{if(!server.HasExited)server.Kill(true);await server.WaitForExitAsync();}
        }
        start.Environment["StayWidget__DevelopmentLogin"]="true";
        using var rejected=Process.Start(start)!;var output=rejected.StandardOutput.ReadToEndAsync();var error=rejected.StandardError.ReadToEndAsync();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try{await rejected.WaitForExitAsync(timeout.Token);}catch{rejected.Kill(true);throw;}
        var messages=(await output)+(await error);Assert.NotEqual(0,rejected.ExitCode);Assert.Contains("Development login must be disabled",messages);
    }
}
