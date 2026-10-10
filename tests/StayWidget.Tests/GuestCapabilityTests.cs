using Microsoft.EntityFrameworkCore;
using StayWidget.Domain;
using StayWidget.Persistence;
using StayWidget.Web;
using Xunit;
namespace StayWidget.Tests;

public sealed class GuestCapabilityTests(TestDatabase fixture) : IClassFixture<TestDatabase>
{
    private static DateOnly Day => DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10);
    private static GuestInput Input(Unit u, string? key = null) => new(u.Id,Day,Day.AddDays(2),2,"Guest","+389 70 000 003",key??BookingService.Secret(),true);
    [Fact] public async Task ViewAndActionScopesAreSeparateAndWithdrawalIsExplicitTerminalAndIdempotent()
    {
        var(owner,p,u)=await fixture.Property();await using var db=fixture.Db();var s=fixture.Service(db);var receipt=await s.Submit(p.Id,Input(u));
        Assert.NotEqual(receipt.Token,receipt.ActionToken);Assert.Null(await s.Status(receipt.ActionToken!));
        await Assert.ThrowsAsync<GuestLinkUnavailable>(()=>s.GuestAction(receipt.Token!,"withdraw",true));
        await Assert.ThrowsAsync<RuleViolation>(()=>s.GuestAction(receipt.ActionToken!,"withdraw",false));
        Assert.Equal("Pending",(await s.Status(receipt.Token!))!.State);
        await s.GuestAction(receipt.ActionToken!,"withdraw",true);await s.GuestAction(receipt.ActionToken!,"withdraw",true);
        Assert.Equal("Withdrawn",(await s.Status(receipt.Token!))!.State);await Assert.ThrowsAsync<RuleViolation>(()=>s.Confirm(owner,receipt.Id,0,100,true));
        Assert.Empty(await db.Allocations.Where(x=>x.PropertyId==p.Id).ToListAsync());
        var r=await db.Requests.SingleAsync(x=>x.Id==receipt.Id);Assert.Equal(BookingService.Hash(receipt.ActionToken!),r.ActionHash);Assert.DoesNotContain(receipt.ActionToken!,r.ProtectedReceipt);
        var json=System.Text.Json.JsonSerializer.Serialize(await s.Status(receipt.Token!));Assert.DoesNotContain("ActionHash",json);Assert.DoesNotContain("000 003",json);Assert.DoesNotContain("OwnerId",json);
    }
    [Fact] public async Task ConfirmationAndWithdrawalRaceHasExactlyOneWinner()
    {
        var(owner,p,u)=await fixture.Property();Receipt receipt;await using(var db=fixture.Db())receipt=await fixture.Service(db).Submit(p.Id,Input(u));
        async Task<bool> Withdraw(){await using var db=fixture.Db();try{await fixture.Service(db).GuestAction(receipt.ActionToken!,"withdraw",true);return true;}catch(RuleViolation){return false;}}
        async Task<bool> Confirm(){await using var db=fixture.Db();try{await fixture.Service(db).Confirm(owner,receipt.Id,0,100,true);return true;}catch(RuleViolation){return false;}}
        Assert.Single(await Task.WhenAll(Withdraw(),Confirm()),x=>x);
        await using var check=fixture.Db();var r=await check.Requests.SingleAsync(x=>x.Id==receipt.Id);
        Assert.Equal(r.State=="Accepted"?1:0,await check.Allocations.CountAsync(x=>x.PropertyId==p.Id&&x.Active));
    }
    [Fact] public async Task GuestCancellationOnlyFlagsInventoryAndOwnerMustResolveIt()
    {
        var(owner,p,u)=await fixture.Property();await using var db=fixture.Db();var s=fixture.Service(db);var receipt=await s.Submit(p.Id,Input(u));
        var r=await s.Confirm(owner,receipt.Id,0,100,true);
        await s.GuestAction(receipt.ActionToken!,"request-cancellation",true);await s.GuestAction(receipt.ActionToken!,"request-cancellation",true);
        Assert.True((await s.Status(receipt.Token!))!.CancellationRequested);Assert.False(await s.Available(u,Day,Day.AddDays(2),2));
        await s.KeepReservation(owner,r.Id,1);Assert.False((await s.Status(receipt.Token!))!.CancellationRequested);Assert.False(await s.Available(u,Day,Day.AddDays(2),2));
        await s.GuestAction(receipt.ActionToken!,"request-cancellation",true);await s.CancelReservation(owner,r.Id,3,true);
        Assert.Equal("Cancelled",(await s.Status(receipt.Token!))!.State);Assert.True(await s.Available(u,Day,Day.AddDays(2),2));
        await Assert.ThrowsAsync<RuleViolation>(()=>s.GuestAction(receipt.ActionToken!,"request-cancellation",true));
    }
    [Fact] public async Task RevocationExpirySuspensionAndOwnerChecksApplyToEveryCapability()
    {
        var(owner,p,u)=await fixture.Property();var(other,_,_)=await fixture.Property();await using var db=fixture.Db();var s=fixture.Service(db);var input=Input(u);var receipt=await s.Submit(p.Id,input);
        await Assert.ThrowsAsync<RuleViolation>(()=>s.RevokeGuestLinks(other,receipt.Id,0,true));await s.RevokeGuestLinks(owner,receipt.Id,0,true);
        Assert.Null(await s.Status(receipt.Token!));await Assert.ThrowsAsync<GuestLinkUnavailable>(()=>s.GuestAction(receipt.ActionToken!,"withdraw",true));Assert.Null((await s.Submit(p.Id,input)).Token);
        var next=await s.Submit(p.Id,Input(u));var expired=fixture.Service(db,new FrozenTime(next.Expires.AddDays(31)));
        Assert.Null(await expired.Status(next.Token!));await Assert.ThrowsAsync<GuestLinkUnavailable>(()=>expired.GuestAction(next.ActionToken!,"withdraw",true));
        db.ChangeTracker.Clear();var property=await db.Properties.SingleAsync(x=>x.Id==p.Id);property.Suspended=true;await db.SaveChangesAsync();
        Assert.Null(await s.Status(next.Token!));await Assert.ThrowsAsync<GuestLinkUnavailable>(()=>s.GuestAction(next.ActionToken!,"withdraw",true));
        await Assert.ThrowsAsync<RuleViolation>(()=>s.Submit(p.Id,Input(u)));
    }
    [Fact] public async Task PropertyDailySafetyCapDoesNotLoseAcceptedRequestsOrBlockReceiptRetries()
    {
        var(_,p,u)=await fixture.Property();await using var db=fixture.Db();var s=fixture.Service(db);var input=Input(u);var first=await s.Submit(p.Id,input);
        for(int i=1;i<100;i++)await s.Submit(p.Id,Input(u));
        Assert.Equal(first,await s.Submit(p.Id,input));await Assert.ThrowsAsync<SubmissionLimited>(()=>s.Submit(p.Id,Input(u)));
        Assert.Equal(100,await db.Requests.CountAsync(x=>x.PropertyId==p.Id));Assert.Empty(await db.Allocations.Where(x=>x.PropertyId==p.Id).ToListAsync());
    }
    [Fact] public async Task EmbedSettingsRejectStaleTenantAndUnattestedChanges()
    {
        var(owner,p,_)=await fixture.Property();var(other,_,_)=await fixture.Property();await using var db=fixture.Db();var s=fixture.Service(db);
        await Assert.ThrowsAsync<RuleViolation>(()=>s.SaveEmbedSettings(owner,0,"https://www.example.com","#12616A",false,false));
        await s.SaveEmbedSettings(owner,0,"https://www.example.com\nhttps://example.com","#12616A",true,false);
        await Assert.ThrowsAsync<RuleViolation>(()=>s.SaveEmbedSettings(owner,0,"https://other.example","#12616A",true,false));
        await using var check=fixture.Db();Assert.Equal(new[]{"https://www.example.com","https://example.com"},(await check.Properties.SingleAsync(x=>x.Id==p.Id)).EmbedOrigins);
        Assert.Empty((await check.Properties.SingleAsync(x=>x.OwnerId==other)).EmbedOrigins);
    }
}

public sealed class EmbedPolicyTests
{
    [Theory][InlineData("https://*.example.com")][InlineData("https://example.com/path")][InlineData("https://user:pass@example.com")][InlineData("https://example.com/?q=1")][InlineData("http://example.com")][InlineData("https://example.com/#fragment")]
    public void UnsafeOrNonexactOriginsAreRejected(string value)=>Assert.Throws<RuleViolation>(()=>EmbedPolicy.Origin(value,false));
    [Fact] public void CanonicalHttpsAndDevelopmentLoopbackOriginsAreExplicit()
    {Assert.Equal("https://www.example.com",EmbedPolicy.Origin("https://WWW.Example.com:443/",false));Assert.Equal("http://127.0.0.1:6280",EmbedPolicy.Origin("http://127.0.0.1:6280",true));Assert.Throws<RuleViolation>(()=>EmbedPolicy.Origin("http://127.0.0.1:6280",false));}
    [Theory][InlineData("#ffffff")][InlineData("#12616A;}")][InlineData("red")]
    public void UnreadableOrInjectedThemeValuesAreRejected(string color)=>Assert.Throws<RuleViolation>(()=>EmbedPolicy.Accent(color));
    [Fact] public void SubmissionGuardUsesPrivateShortLivedBucketsAndResetsAtWindowBoundary()
    {
        var clock=new MovingTime(new DateTimeOffset(2026,10,10,12,0,0,TimeSpan.Zero));var guard=new SubmissionGuard(clock);
        for(int i=0;i<5;i++)guard.Acquire("192.0.2.1");Assert.Throws<SubmissionLimited>(()=>guard.Acquire("192.0.2.1"));guard.Acquire("192.0.2.2");
        Assert.NotEqual(guard.Identity("192.0.2.1"),new SubmissionGuard(clock).Identity("192.0.2.1"));
        clock.Now=clock.Now.AddMinutes(10);guard.Acquire("192.0.2.1");
    }
}
public sealed class MovingTime(DateTimeOffset now):TimeProvider {public DateTimeOffset Now {get;set;}=now;public override DateTimeOffset GetUtcNow()=>Now;}
