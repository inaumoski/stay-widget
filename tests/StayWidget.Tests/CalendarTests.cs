using Microsoft.EntityFrameworkCore;
using StayWidget.Domain;
using StayWidget.Persistence;
using StayWidget.Web;
using Xunit;
namespace StayWidget.Tests;
public sealed class CalendarTests(TestDatabase fixture):IClassFixture<TestDatabase>
{
    [Fact] public async Task CalendarUsesSeasonsBookingsBlocksTurnoverButNotPendingRequestsAndExposesNoPrivateData()
    {
        var(owner,p,u)=await fixture.Property();await using var db=fixture.Db();var now=new DateTimeOffset(2028,6,1,12,0,0,TimeSpan.Zero);var s=fixture.Service(db,new FrozenTime(now));
        await s.SaveUnit(owner,u.Id,0,new(u.Name,2,new(2028,6,1),new(2028,7,1),1,28,1,true));
        var booked=await s.Submit(p.Id,new(u.Id,new(2028,6,10),new(2028,6,12),2,"PRIVATE_GUEST","000 000 001",BookingService.Secret(),true));await s.Confirm(owner,booked.Id,0,100,true);
        await s.Submit(p.Id,new(u.Id,new(2028,6,20),new(2028,6,22),2,"PRIVATE_PENDING","000 000 002",BookingService.Secret(),true));
        var block=await s.SaveBlock(owner,Guid.NewGuid(),null,u.Id,new(2028,6,15),new(2028,6,16),"PRIVATE_REASON");
        var calendar=(await s.Calendar(p.Id,u.Id,new(2028,6,1),2))!;bool Free(int day)=>calendar.Nights.Single(x=>x.Date.Day==day).Available;
        Assert.False(Free(1));Assert.False(Free(10));Assert.False(Free(11));Assert.False(Free(12));Assert.True(Free(13));Assert.False(Free(15));Assert.True(Free(16));Assert.True(Free(20));
        var json=System.Text.Json.JsonSerializer.Serialize(calendar);Assert.DoesNotContain("PRIVATE",json);Assert.DoesNotContain("000 000",json);Assert.DoesNotContain("ReservationId",json);Assert.DoesNotContain("OwnerId",json);
        await s.RemoveBlock(owner,block.Id,block.Revision);Assert.True((await s.Calendar(p.Id,u.Id,new(2028,6,1),2))!.Nights.Single(x=>x.Date.Day==15).Available);
        Assert.All((await s.Calendar(p.Id,u.Id,new(2028,6,1),3))!.Nights,x=>Assert.False(x.Available));
        Assert.Null(await s.Calendar(Guid.NewGuid(),u.Id,new(2028,6,1),2));
        await Assert.ThrowsAsync<RuleViolation>(()=>s.Calendar(p.Id,u.Id,new(2028,6,15),2));await Assert.ThrowsAsync<RuleViolation>(()=>s.Calendar(p.Id,u.Id,new(2030,6,1),2));
    }
    [Fact] public async Task CalendarDefaultAndSavedPreferenceAreServerControlledWithTenantAndRevisionChecks()
    {
        var(owner,p,u)=await fixture.Property();Assert.True(p.ShowAvailabilityCalendar);await using var db=fixture.Db();var s=fixture.Service(db);var month=AvailabilityCalendar.Month(StayRules.Today(DateTimeOffset.UtcNow,p.Zone));
        Assert.NotNull(await s.Calendar(p.Id,u.Id,month,2));await s.SaveEmbedSettings(owner,0,"","#12616A",false,true,false);
        Assert.Null(await s.Calendar(p.Id,u.Id,month,2));await Assert.ThrowsAsync<RuleViolation>(()=>s.SaveEmbedSettings(owner,0,"","#12616A",false,true,true));await Assert.ThrowsAsync<RuleViolation>(()=>s.SaveEmbedSettings(Guid.NewGuid(),1,"","#12616A",false,true,true));
        await s.SaveEmbedSettings(owner,1,"","#12616A",false,true,true);Assert.NotNull(await s.Calendar(p.Id,u.Id,month,2));
        var unit=await db.Units.SingleAsync(x=>x.Id==u.Id);await s.SaveUnit(owner,u.Id,unit.Revision,new(unit.Name,2,unit.OpenFrom,unit.OpenTo,1,28,0,false));Assert.Null(await s.Calendar(p.Id,u.Id,month,2));
    }
    [Fact] public void NovemberDecemberIncludesDecember31AndExcludesJanuary1()
    {
        var unit=new Unit{Capacity=2,OpenFrom=new(2026,11,1),OpenTo=new(2027,1,1)};var today=new DateOnly(2026,10,10);
        var december=AvailabilityCalendar.Build(unit,today,new(2026,12,1),2,[]);Assert.Equal(31,december.Nights.Count);Assert.True(december.Nights.Last().Available);
        Assert.All(AvailabilityCalendar.Build(unit,today,new(2027,1,1),2,[]).Nights,x=>Assert.False(x.Available));
        Assert.All(AvailabilityCalendar.Build(unit,today,new(2026,10,1),2,[]).Nights,x=>Assert.False(x.Available));
        unit.OpenFrom=new(2028,2,1);unit.OpenTo=new(2028,3,1);Assert.Equal(29,AvailabilityCalendar.Build(unit,new(2028,2,1),new(2028,2,1),2,[]).Nights.Count);
    }
}
