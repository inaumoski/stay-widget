using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StayWidget.Domain;
using StayWidget.Persistence;
using StayWidget.Web;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization=true)]
namespace StayWidget.Tests;

public sealed class TestDatabase : IAsyncLifetime
{
    public string Connection {get;private set;}="";
    private string admin=""; private string name="";
    public IDataProtectionProvider Protection {get;}=new EphemeralDataProtectionProvider();
    public async Task InitializeAsync()
    {
        var raw=Environment.GetEnvironmentVariable("STAYWIDGET_TEST_ADMIN")??throw new InvalidOperationException("Run tools/run-tests.py against the marked StayWidget loopback cluster.");
        var b=new NpgsqlConnectionStringBuilder(raw);
        if(b.Host!="127.0.0.1"||b.Port!=55439||b.Username!="staywidget_dev"||b.Database!="postgres")throw new InvalidOperationException("Only the dedicated StayWidget test cluster is permitted.");
        admin=b.ConnectionString;name="staywidget_test_"+Guid.NewGuid().ToString("N");
        await using var c=new NpgsqlConnection(admin);await c.OpenAsync();
        await using(var cmd=new NpgsqlCommand($"CREATE DATABASE {name}",c))await cmd.ExecuteNonQueryAsync();
        b.Database=name;Connection=b.ConnectionString;
        await using var db=Db();await db.Database.MigrateAsync();
    }
    public StayDb Db()=>new(new DbContextOptionsBuilder<StayDb>().UseNpgsql(Connection).Options);
    public BookingService Service(StayDb db,TimeProvider? time=null)=>new(db,time??TimeProvider.System,Protection);
    public async Task<(Guid Owner, Property Property, Unit Unit)> Property()
    {
        await using var db=Db();var service=Service(db);var owner=await service.User("urn:test",Guid.NewGuid().ToString(),"Test owner");
        var p=await service.Setup(owner,"Test Cottage","+389 70 000 000","Europe/Skopje","Contact the host to agree the price and cancellation terms.","Garden Cottage",2);
        return(owner,p,await db.Units.SingleAsync(x=>x.PropertyId==p.Id));
    }
    public async Task DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();if(!name.StartsWith("staywidget_test_",StringComparison.Ordinal))return;
        await using var c=new NpgsqlConnection(admin);await c.OpenAsync();await using var cmd=new NpgsqlCommand($"DROP DATABASE {name} WITH (FORCE)",c);await cmd.ExecuteNonQueryAsync();
    }
}
public sealed class FrozenTime(DateTimeOffset now):TimeProvider { public override DateTimeOffset GetUtcNow()=>now; }
public sealed class InventoryTests(TestDatabase fixture):IClassFixture<TestDatabase>
{
    private GuestInput Input(Unit u,string? key=null)=>new(u.Id,DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10),DateOnly.FromDateTime(DateTime.UtcNow).AddDays(12),2,"Guest","+389 70 000 001",key??BookingService.Secret(),true);
    [Fact] public async Task PendingRequestsDoNotHoldDatesAndDuplicateRetriesReturnSameReceipt()
    {
        var(_,p,u)=await fixture.Property();var input=Input(u);await using var db=fixture.Db();var svc=fixture.Service(db);
        var first=await svc.Submit(p.Id,input);var again=await svc.Submit(p.Id,input);
        Assert.Equal(first,again);Assert.True(await svc.Available(u,input.Arrival,input.Departure,2));Assert.Equal(1,await db.Requests.CountAsync(x=>x.PropertyId==p.Id));Assert.Equal(0,await db.Allocations.CountAsync(x=>x.PropertyId==p.Id));
        await Assert.ThrowsAsync<RuleViolation>(()=>svc.Submit(p.Id,input with{Name="Changed guest"}));
    }
    [Fact] public async Task ConcurrentConflictingConfirmationHasExactlyOneWinner()
    {
        var(owner,p,u)=await fixture.Property();Receipt a,b;await using(var db=fixture.Db()){var svc=fixture.Service(db);a=await svc.Submit(p.Id,Input(u));b=await svc.Submit(p.Id,Input(u));}
        async Task<bool> Accept(Guid id){await using var db=fixture.Db();try{await fixture.Service(db).Confirm(owner,id,0,120,true);return true;}catch(RuleViolation){return false;}}
        var results=await Task.WhenAll(Accept(a.Id),Accept(b.Id));Assert.Single(results,x=>x);
        await using var check=fixture.Db();Assert.Equal(1,await check.Reservations.CountAsync(x=>x.PropertyId==p.Id));Assert.Equal(1,await check.Allocations.CountAsync(x=>x.PropertyId==p.Id));Assert.Equal(1,await check.Requests.CountAsync(x=>x.PropertyId==p.Id&&x.State=="Pending"));
    }
    [Fact] public async Task ConfirmationRetriesAreIdempotentAndAvailabilityExcludesOccupiedNights()
    {
        var(owner,p,u)=await fixture.Property();var input=Input(u);Receipt receipt;
        await using(var db=fixture.Db())receipt=await fixture.Service(db).Submit(p.Id,input);
        Guid id;await using(var db=fixture.Db())id=(await fixture.Service(db).Confirm(owner,receipt.Id,0,100,true)).Id;
        await using var check=fixture.Db();var svc=fixture.Service(check);Assert.Equal(id,(await svc.Confirm(owner,receipt.Id,0,100,true)).Id);
        Assert.False(await svc.Available(u,input.Arrival,input.Departure,2));Assert.True(await svc.Available(u,input.Departure,input.Departure.AddDays(2),2));
        await Assert.ThrowsAsync<RuleViolation>(()=>svc.Confirm(owner,receipt.Id,0,200,true));
    }
    [Fact] public async Task ExpiryIsCheckedWithoutAWorkerAndStatusDoesNotExposeContacts()
    {
        var(owner,p,u)=await fixture.Property();Receipt receipt;await using(var db=fixture.Db())receipt=await fixture.Service(db).Submit(p.Id,Input(u));
        await using var check=fixture.Db();var service=fixture.Service(check,new FrozenTime(receipt.Expires));await Assert.ThrowsAsync<RuleViolation>(()=>service.Confirm(owner,receipt.Id,0,100,true));
        var status=await service.Status(receipt.Token!);Assert.Equal("Expired",status!.State);Assert.Null(await service.Status(BookingService.Secret()));
        var json=System.Text.Json.JsonSerializer.Serialize(status);Assert.DoesNotContain("+389 70 000 001",json);Assert.DoesNotContain("\"Name\"",json);
    }
    [Fact] public async Task AnotherOwnerCannotConfirmOrMixUnitsAcrossProperties()
    {
        var(_,p,u)=await fixture.Property();var(other,q,v)=await fixture.Property();Receipt receipt;await using(var db=fixture.Db())receipt=await fixture.Service(db).Submit(p.Id,Input(u));
        await using var check=fixture.Db();var service=fixture.Service(check);await Assert.ThrowsAsync<RuleViolation>(()=>service.Confirm(other,receipt.Id,0,100,true));await Assert.ThrowsAsync<RuleViolation>(()=>service.Submit(q.Id,Input(u)));
        Assert.Equal("Pending",(await check.Requests.SingleAsync(x=>x.Id==receipt.Id)).State);
    }
    [Fact] public async Task DatabaseRejectsOverlapEvenWithoutApplicationAvailabilityCheck()
    {
        var(owner,p,u)=await fixture.Property();Receipt a,b;await using(var db=fixture.Db()){a=await fixture.Service(db).Submit(p.Id,Input(u));b=await fixture.Service(db).Submit(p.Id,Input(u));}
        await using(var db=fixture.Db())await fixture.Service(db).Confirm(owner,a.Id,0,100,true);
        await using var bypass=fixture.Db();var input=Input(u);var res=new Reservation{PropertyId=p.Id,UnitId=u.Id,RequestId=b.Id,Arrival=input.Arrival,Departure=input.Departure,Amount=100,Currency="EUR",TermsVersion="1",Confirmed=DateTimeOffset.UtcNow};
        bypass.Reservations.Add(res);bypass.Allocations.Add(new InventoryAllocation{PropertyId=p.Id,UnitId=u.Id,ReservationId=res.Id,Start=res.Arrival,End=res.Departure});
        var ex=await Assert.ThrowsAsync<DbUpdateException>(()=>bypass.SaveChangesAsync());Assert.Equal("23P01",Assert.IsType<PostgresException>(ex.InnerException).SqlState);
        await using var check=fixture.Db();Assert.Equal(1,await check.Reservations.CountAsync(x=>x.PropertyId==p.Id));
    }
    [Fact] public async Task RequestRejectsCapacityInvalidMoneyAndUnagreedConfirmation()
    {
        var(owner,p,u)=await fixture.Property();await using var db=fixture.Db();var svc=fixture.Service(db);
        await Assert.ThrowsAsync<RuleViolation>(()=>svc.Submit(p.Id,Input(u) with{Guests=3}));var r=await svc.Submit(p.Id,Input(u));
        await Assert.ThrowsAsync<RuleViolation>(()=>svc.Confirm(owner,r.Id,0,0,false));await Assert.ThrowsAsync<RuleViolation>(()=>svc.Confirm(owner,r.Id,0,1.001m,true));await Assert.ThrowsAsync<RuleViolation>(()=>svc.Confirm(owner,r.Id,1,1,true));
    }
    [Fact] public async Task SeparateSubjectsNeverMergeByDisplayName()
    {
        await using var db=fixture.Db();var s=fixture.Service(db);var a=await s.User("urn:first","same","Same name");var b=await s.User("urn:second","same","Same name");Assert.NotEqual(a,b);Assert.Equal(a,await s.User("urn:first","same","New display"));
    }
}
public sealed class DateTests
{
    [Fact] public void CheckoutIsExclusive(){Assert.False(StayRules.Overlaps(new(2026,6,10),new(2026,6,12),new(2026,6,12),new(2026,6,14)));Assert.True(StayRules.Overlaps(new(2026,6,10),new(2026,6,13),new(2026,6,12),new(2026,6,14)));}
    [Fact] public void StayUsesCalendarNightsAcrossDst(){StayRules.Validate(new(2026,10,24),new(2026,10,26),2,2,new(2026,10,10),new(2026,10,11),new(2027,10,11));}
    [Theory][InlineData(0,2)][InlineData(29,2)][InlineData(2,0)][InlineData(2,3)]public void InvalidStayOrCapacityFails(int nights,int guests){var a=new DateOnly(2026,10,20);Assert.Throws<RuleViolation>(()=>StayRules.Validate(a,a.AddDays(nights),guests,2,new(2026,10,10),new(2026,10,11),new(2027,10,11)));}
    [Fact] public void DeadlineUsesEarlierArrivalMidnightOr48Hours(){var now=new DateTimeOffset(2026,10,10,12,0,0,TimeSpan.Zero);Assert.Equal(new DateTimeOffset(2026,10,10,22,0,0,TimeSpan.Zero),StayRules.Deadline(now,new(2026,10,11),"Europe/Skopje"));Assert.Equal(now.AddHours(48),StayRules.Deadline(now,new(2026,10,20),"Europe/Skopje"));}
}
