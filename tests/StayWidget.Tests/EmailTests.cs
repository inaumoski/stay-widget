using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using StayWidget.Domain;
using StayWidget.Persistence;
using StayWidget.Web;
using Xunit;

namespace StayWidget.Tests;
public sealed class EmailTests : IAsyncLifetime
{
    private readonly TestDatabase fixture = new();
    public Task InitializeAsync() => fixture.InitializeAsync();
    public Task DisposeAsync() => fixture.DisposeAsync();
    private static GuestInput Input(Unit u) => new(u.Id,DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10),DateOnly.FromDateTime(DateTime.UtcNow).AddDays(12),2,"Email guest","+389 70 000 123",BookingService.Secret(),true,Email:"guest@example.invalid");
    private ConfirmationDispatcher Dispatcher(StayDb db,IConfirmationTransport transport,TimeProvider? time=null,EmailSettings? settings=null) => new(db,time??TimeProvider.System,fixture.Protection,transport,settings??new(){Mode="Sink"});

    [Fact] public async Task ConfirmationQueuesOnceWithPrivateCancellationLinkAndBlocksNightsImmediately()
    {
        var(owner,p,u)=await fixture.Property();await using var db=fixture.Db();var s=new BookingService(db,TimeProvider.System,fixture.Protection,site:new("https://staywidget.funtoolie.com"));var input=Input(u);
        var receipt=await s.Submit(p.Id,input);Assert.Empty(await db.ConfirmationEmails.ToListAsync());Assert.True(await s.Available(u,input.Arrival,input.Departure,2));
        var reservation=await s.Confirm(owner,receipt.Id,0,120,true);await s.Confirm(owner,receipt.Id,0,120,true);
        var mail=await db.ConfirmationEmails.SingleAsync();Assert.Equal("Queued",mail.State);Assert.DoesNotContain(input.Email!,mail.ProtectedMessage);Assert.DoesNotContain(receipt.ActionToken!,mail.ProtectedMessage);
        var message=JsonSerializer.Deserialize<ConfirmationMessage>(fixture.Protection.CreateProtector("StayWidget.ConfirmationEmail.v1").Unprotect(mail.ProtectedMessage))!;
        Assert.Equal(input.Email,message.To);Assert.StartsWith("Reservation confirmed",message.Text);Assert.Contains("Owner contact: "+p.Contact,message.Text);Assert.Equal("https://staywidget.funtoolie.com/GuestStatus#"+receipt.Token+"."+receipt.ActionToken,message.Link);
        Assert.False(await s.Available(u,input.Arrival,input.Departure,2));var cal=(await s.Calendar(p.Id,u.Id,AvailabilityCalendar.Month(input.Arrival),2))!;Assert.All(cal.Nights.Where(x=>x.Date>=input.Arrival&&x.Date<input.Departure),x=>Assert.False(x.Available));
        await s.GuestAction(receipt.ActionToken!,"request-cancellation",true);Assert.False(await s.Available(u,input.Arrival,input.Departure,2));
        var current=await db.Reservations.SingleAsync(x=>x.Id==reservation.Id);await s.CancelReservation(owner,current.Id,current.Revision,true);Assert.True(await s.Available(u,input.Arrival,input.Departure,2));
        Assert.DoesNotContain(input.Email!,JsonSerializer.Serialize(await s.Status(receipt.Token!)));
    }

    [Fact] public async Task EmailValidationAndRetriesPreserveIdentityAndPendingTokenRecovery()
    {
        var(owner,p,u)=await fixture.Property();await using var db=fixture.Db();var input=Input(u);var now=DateTimeOffset.UtcNow;var s=fixture.Service(db,new FrozenTime(now));
        foreach(var invalid in new[]{"","a\r\nb@example.com","Guest <guest@example.com>","a@example.com,b@example.com"})await Assert.ThrowsAsync<RuleViolation>(()=>s.Submit(p.Id,input with{Email=invalid}));
        var receipt=await s.Submit(p.Id,input);Assert.Equal(receipt,await s.Submit(p.Id,input));await Assert.ThrowsAsync<RuleViolation>(()=>s.Submit(p.Id,input with{Email="other@example.invalid"}));
        var later=fixture.Service(db,new FrozenTime(now.AddHours(25)));await later.Maintain();Assert.NotEmpty((await db.Requests.SingleAsync()).ProtectedReceipt);await later.Confirm(owner,receipt.Id,0,0,true);Assert.Single(await db.ConfirmationEmails.ToListAsync());
    }

    [Fact] public async Task RateLimitedDeliveryRetriesBoundedlyAndNeverChangesReservation()
    {
        var(owner,p,u)=await fixture.Property();await using var db=fixture.Db();var s=fixture.Service(db);var receipt=await s.Submit(p.Id,Input(u));await s.Confirm(owner,receipt.Id,0,100,true);
        var transport=new FakeTransport(new("Queued","ProviderRateLimited"));var now=DateTimeOffset.UtcNow;
        for(int i=0;i<5;i++)Assert.True(await Dispatcher(db,transport,new FrozenTime(now.AddHours(i*3))).DispatchOne());
        var mail=await db.ConfirmationEmails.SingleAsync();Assert.Equal(5,mail.Attempts);Assert.Equal("Failed",mail.State);Assert.Empty(mail.ProtectedMessage);Assert.Single(transport.Ids.Distinct());Assert.False(await Dispatcher(db,transport,new FrozenTime(now.AddHours(8))).DispatchOne());
        Assert.Equal("Confirmed",(await db.Reservations.SingleAsync()).State);Assert.Single(await db.Allocations.Where(x=>x.Active).ToListAsync());
    }

    [Fact] public async Task ConcurrentDispatchAndIndeterminateAttemptCannotReplayEmail()
    {
        var(owner,p,u)=await fixture.Property();await using var db=fixture.Db();var s=fixture.Service(db);await s.Confirm(owner,(await s.Submit(p.Id,Input(u))).Id,0,100,true);
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var transport=new BlockingTransport(entered,release);await using var second=fixture.Db();var first=Dispatcher(db,transport).DispatchOne();await entered.Task;
        Assert.False(await Dispatcher(second,transport).DispatchOne());release.SetResult();await Assert.ThrowsAsync<OperationCanceledException>(()=>first);
        await using var check=fixture.Db();var mail=await check.ConfirmationEmails.SingleAsync();Assert.Equal("Unknown",mail.State);Assert.Equal(1,mail.Attempts);Assert.False(await Dispatcher(check,new FakeTransport(new("Accepted"))).DispatchOne());Assert.Equal("Confirmed",(await check.Reservations.SingleAsync()).State);
    }

    [Fact] public async Task RevokedLinksSuppressDeliveryAndRetentionRemovesOutboxContactData()
    {
        var(owner,p,u)=await fixture.Property();await using var db=fixture.Db();var s=fixture.Service(db);var receipt=await s.Submit(p.Id,Input(u));await s.Confirm(owner,receipt.Id,0,100,true);await s.RevokeGuestLinks(owner,receipt.Id,1,true);
        var transport=new FakeTransport(new("Accepted"));Assert.True(await Dispatcher(db,transport).DispatchOne());Assert.Empty(transport.Ids);Assert.Equal("Cancelled",(await db.ConfirmationEmails.SingleAsync()).State);Assert.Empty((await db.ConfirmationEmails.SingleAsync()).ProtectedMessage);
        await fixture.Service(db,new FrozenTime(DateTimeOffset.UtcNow.AddDays(31))).Maintain();Assert.Empty(await db.ConfirmationEmails.ToListAsync());
    }

    [Fact] public async Task BrevoRequestEncodesUntrustedContentAndDistinguishesAcceptanceFromDelivery()
    {
        var handler=new CaptureHandler();var client=new HttpClient(handler);var transport=new ConfirmationTransport(new(){Mode="Brevo",ApiKey="synthetic-key",Sender="noreply@funtoolie.com"},new ClientFactory(client));
        var message=new ConfirmationMessage("guest@example.invalid","Reservation confirmed <script>unsafe</script>","https://staywidget.funtoolie.com/GuestStatus#synthetic.private");
        Assert.Equal("Accepted",(await transport.Send(Guid.NewGuid(),message,default)).State);Assert.Equal("https://api.brevo.com/v3/smtp/email",handler.Url);Assert.Equal("synthetic-key",handler.Key);
        using var body=JsonDocument.Parse(handler.Body);Assert.Equal("noreply@funtoolie.com",body.RootElement.GetProperty("sender").GetProperty("email").GetString());Assert.Contains("&lt;script&gt;",body.RootElement.GetProperty("htmlContent").GetString());Assert.Single(body.RootElement.GetProperty("to").EnumerateArray());Assert.False(body.RootElement.TryGetProperty("bcc",out _));
        handler.Status=HttpStatusCode.TooManyRequests;Assert.Equal("Queued",(await transport.Send(Guid.NewGuid(),message,default)).State);handler.Status=HttpStatusCode.InternalServerError;Assert.Equal("Unknown",(await transport.Send(Guid.NewGuid(),message,default)).State);
        Assert.Throws<InvalidOperationException>(()=>new EmailSettings{Mode="Brevo",ApiKey="synthetic"}.Validate(true));Assert.Throws<InvalidOperationException>(()=>new EmailSettings{Mode="Sink",SinkDirectory=Path.GetTempPath()}.Validate(false));
    }
    [Fact] public async Task MissingEmailKeysDoNotUndoOwnerConfirmation()
    {
        var(owner,p,u)=await fixture.Property();await using var db=fixture.Db();var receipt=await fixture.Service(db).Submit(p.Id,Input(u));
        var otherKeys=new EphemeralDataProtectionProvider();await new BookingService(db,TimeProvider.System,otherKeys).Confirm(owner,receipt.Id,0,100,true);
        Assert.Equal("Confirmed",(await db.Reservations.SingleAsync()).State);Assert.Equal("Failed",(await db.ConfirmationEmails.SingleAsync()).State);Assert.Single(await db.Allocations.Where(x=>x.Active).ToListAsync());
    }

    [Fact] public async Task DevelopmentSinkWritesConfirmationWithoutAnExternalCall()
    {
        var(owner,p,u)=await fixture.Property();await using var db=fixture.Db();var s=fixture.Service(db);var receipt=await s.Submit(p.Id,Input(u));await s.Confirm(owner,receipt.Id,0,100,true);
        var folder=Path.Combine(Path.GetTempPath(),"staywidget-email-test-"+Guid.NewGuid().ToString("N"));var settings=new EmailSettings{Mode="Sink",SinkDirectory=folder};settings.Validate(true);
        var mail=await db.ConfirmationEmails.SingleAsync();var file=Path.Combine(folder,mail.Id.ToString("N")+".json");
        try
        {
            var transport=new ConfirmationTransport(settings,new ClientFactory(new HttpClient(new CaptureHandler())));Assert.True(await Dispatcher(db,transport,settings:settings).DispatchOne());
            using var saved=JsonDocument.Parse(await File.ReadAllTextAsync(file));Assert.Equal("noreply@funtoolie.com",saved.RootElement.GetProperty("Sender").GetString());Assert.Contains("Owner contact: "+p.Contact,saved.RootElement.GetProperty("Text").GetString());Assert.Contains(receipt.ActionToken!,saved.RootElement.GetProperty("Text").GetString());Assert.Equal("LocalPreview",(await db.ConfirmationEmails.SingleAsync()).State);Assert.Empty((await db.ConfirmationEmails.SingleAsync()).ProtectedMessage);
        }
        finally { if(File.Exists(file))File.Delete(file);if(Directory.Exists(folder))Directory.Delete(folder); }
    }

    [Fact] public async Task DailyAttemptBudgetDefersMessagesWithoutLosingBookings()
    {
        var(owner,p,u)=await fixture.Property();await using var db=fixture.Db();var s=fixture.Service(db);var first=Input(u);var second=Input(u) with{Arrival=first.Arrival.AddDays(3),Departure=first.Departure.AddDays(3)};
        await s.Confirm(owner,(await s.Submit(p.Id,first)).Id,0,100,true);await s.Confirm(owner,(await s.Submit(p.Id,second)).Id,0,100,true);
        var transport=new FakeTransport(new("Accepted"));var settings=new EmailSettings{Mode="Sink",DailyAttemptLimit=1};Assert.True(await Dispatcher(db,transport,settings:settings).DispatchOne());Assert.True(await Dispatcher(db,transport,settings:settings).DispatchOne());Assert.Single(transport.Ids);
        var pending=await db.ConfirmationEmails.SingleAsync(x=>x.State=="Queued");Assert.Equal(0,pending.Attempts);Assert.NotEmpty(pending.ProtectedMessage);Assert.Equal(DateTimeOffset.UtcNow.UtcDateTime.Date.AddDays(1),pending.NextAttempt.UtcDateTime);Assert.Equal(2,await db.Reservations.CountAsync(x=>x.State=="Confirmed"));
    }

    private sealed class FakeTransport(EmailResult result):IConfirmationTransport
    { public List<Guid> Ids{get;}=[];public Task<EmailResult> Send(Guid id,ConfirmationMessage message,CancellationToken cancellation){Ids.Add(id);return Task.FromResult(result);} }
    private sealed class BlockingTransport(TaskCompletionSource entered,TaskCompletionSource release):IConfirmationTransport
    { public async Task<EmailResult> Send(Guid id,ConfirmationMessage message,CancellationToken cancellation){entered.SetResult();await release.Task;throw new OperationCanceledException();} }
    private sealed class ClientFactory(HttpClient client):IHttpClientFactory { public HttpClient CreateClient(string name)=>client; }
    private sealed class CaptureHandler:HttpMessageHandler
    {
        public HttpStatusCode Status=HttpStatusCode.Created;public string Url="",Key="",Body="";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellation)
        {Url=request.RequestUri!.ToString();Key=request.Headers.GetValues("api-key").Single();Body=await request.Content!.ReadAsStringAsync(cancellation);return new(Status){Content=new StringContent("{}")};}
    }
}
