using Microsoft.EntityFrameworkCore;
using Npgsql;
using StayWidget.Domain;
using StayWidget.Persistence;
using StayWidget.Web;
using Xunit;
namespace StayWidget.Tests;

public sealed class LifecycleTests(TestDatabase fixture) : IClassFixture<TestDatabase>
{
    private static DateOnly Day => DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10);
    private static ManualStay Stay(Unit u, DateOnly? day = null) => new(u.Id, day ?? Day, (day ?? Day).AddDays(2), 2, "Guest", "+389 70 000 002", 100, true);
    private static UnitSettings Settings(Unit u, int turnover = 0) => new(u.Name, u.Capacity, u.OpenFrom, u.OpenTo, u.MinNights, u.MaxNights, turnover, true);

    [Fact] public async Task ManualBookingBlockRaceHasOneWinnerAndDatabaseEnforcesBookingBlockOverlap()
    {
        var (owner,p,u)=await fixture.Property();
        async Task<bool> Book() { await using var db=fixture.Db(); try { await fixture.Service(db).ManualBooking(owner,Guid.NewGuid(),Stay(u));return true;}catch(RuleViolation){return false;} }
        async Task<bool> Block() { await using var db=fixture.Db(); try { await fixture.Service(db).SaveBlock(owner,Guid.NewGuid(),null,u.Id,Day,Day.AddDays(2),"Outside booking");return true;}catch(RuleViolation){return false;} }
        Assert.Single(await Task.WhenAll(Book(),Block()),x=>x);
        await using var check=fixture.Db(); Assert.Equal(1,await check.Allocations.CountAsync(x=>x.PropertyId==p.Id&&x.Active));
        check.Allocations.Add(new(){PropertyId=p.Id,UnitId=u.Id,Start=Day,End=Day.AddDays(1),Active=true,Reason="Bypass"});
        var e=await Assert.ThrowsAsync<DbUpdateException>(()=>check.SaveChangesAsync()); Assert.Equal("23P01",Assert.IsType<PostgresException>(e.InnerException).SqlState);
    }

    [Fact] public async Task ManualRetriesCancellationAndStaleEditsPreserveHistoryAndReleaseDates()
    {
        var(owner,p,u)=await fixture.Property();var id=Guid.NewGuid();await using var db=fixture.Db();var s=fixture.Service(db);
        var r=await s.ManualBooking(owner,id,Stay(u));Assert.Equal(r.Id,(await s.ManualBooking(owner,id,Stay(u))).Id);
        await Assert.ThrowsAsync<RuleViolation>(()=>s.ManualBooking(owner,id,Stay(u) with{Amount=101}));
        await s.EditReservation(owner,id,0,Stay(u,Day.AddDays(3)));
        await Assert.ThrowsAsync<RuleViolation>(()=>s.EditReservation(owner,id,0,Stay(u,Day.AddDays(6))));
        await Assert.ThrowsAsync<RuleViolation>(()=>s.CancelReservation(owner,id,0,true));
        await s.CancelReservation(owner,id,1,true);await s.CancelReservation(owner,id,1,true);
        await using var check=fixture.Db();var current=await check.Reservations.SingleAsync(x=>x.Id==id);
        Assert.Equal("Cancelled",current.State);Assert.Equal(Day.AddDays(3),current.Arrival);
        Assert.False((await check.Allocations.SingleAsync(x=>x.ReservationId==id)).Active);
        Assert.True(await fixture.Service(check).Available(u,Day.AddDays(3),Day.AddDays(5),2));
    }

    [Fact] public async Task EditConflictRollsBackAndCannotRemoveBookingAllocationAsBlock()
    {
        var(owner,p,u)=await fixture.Property();await using var db=fixture.Db();var s=fixture.Service(db);
        var r=await s.ManualBooking(owner,Guid.NewGuid(),Stay(u));
        await s.SaveBlock(owner,Guid.NewGuid(),null,u.Id,Day.AddDays(3),Day.AddDays(5),"Maintenance");
        await Assert.ThrowsAsync<RuleViolation>(()=>s.EditReservation(owner,r.Id,0,Stay(u,Day.AddDays(3))));
        var a=await db.Allocations.AsNoTracking().SingleAsync(x=>x.ReservationId==r.Id);
        await Assert.ThrowsAsync<RuleViolation>(()=>s.RemoveBlock(owner,a.Id,0));
        await using var check=fixture.Db();Assert.Equal(Day,(await check.Reservations.SingleAsync(x=>x.Id==r.Id)).Arrival);
        Assert.Equal(Day,(await check.Allocations.SingleAsync(x=>x.ReservationId==r.Id)).Start);
    }

    [Fact] public async Task AcceptedEnquirySnapshotSurvivesEditsAndGuestSeesCurrentDatesAndCancellation()
    {
        var(owner,p,u)=await fixture.Property();await using var db=fixture.Db();var s=fixture.Service(db);
        var receipt=await s.Submit(p.Id,new(u.Id,Day,Day.AddDays(2),2,"Guest","+389 70 000 002",BookingService.Secret(),true));
        var r=await s.Confirm(owner,receipt.Id,0,100,true);
        await s.EditReservation(owner,r.Id,0,Stay(u,Day.AddDays(4)));
        Assert.Equal(Day.AddDays(4),(await s.Status(receipt.Token!))!.Arrival);
        await using var check=fixture.Db();Assert.Equal(Day,(await check.Requests.SingleAsync(x=>x.Id==receipt.Id)).Arrival);
        await s.CancelReservation(owner,r.Id,1,true);Assert.Equal("Cancelled",(await s.Status(receipt.Token!))!.State);
        await Assert.ThrowsAsync<RuleViolation>(()=>s.Confirm(owner,receipt.Id,1,100,true));
    }

    [Fact] public async Task DraftUnitsFiveUnitLimitAndSeasonChangesPreserveReservations()
    {
        var(owner,p,u)=await fixture.Property();await using var db=fixture.Db();var s=fixture.Service(db);
        var booked=await s.ManualBooking(owner,Guid.NewGuid(),Stay(u));
        var closed=await s.SaveUnit(owner,u.Id,0,Settings(u) with{Enabled=false,OpenFrom=Day.AddDays(20)});
        Assert.False(closed.Enabled);Assert.Equal("Confirmed",(await db.Reservations.SingleAsync(x=>x.Id==booked.Id)).State);
        for(int i=0;i<4;i++){var draft=await s.SaveUnit(owner,Guid.NewGuid(),null,Settings(u) with{Name="Unit "+i});Assert.False(draft.Enabled);}
        await Assert.ThrowsAsync<RuleViolation>(()=>s.SaveUnit(owner,Guid.NewGuid(),null,Settings(u) with{Name="Sixth"}));
        await Assert.ThrowsAsync<RuleViolation>(()=>s.SaveUnit(owner,u.Id,1,Settings(closed) with{Capacity=1}));
        await Assert.ThrowsAsync<RuleViolation>(()=>s.SaveUnit(owner,u.Id,0,Settings(u)));
        Assert.Equal(5,await db.Units.CountAsync(x=>x.PropertyId==p.Id));
    }

    [Fact] public async Task TurnoverChangesCannotCollideAndRulesAreEnforcedAtConfirmation()
    {
        var(owner,p,u)=await fixture.Property();await using var db=fixture.Db();var s=fixture.Service(db);
        await s.ManualBooking(owner,Guid.NewGuid(),Stay(u));await s.ManualBooking(owner,Guid.NewGuid(),Stay(u,Day.AddDays(2)));
        await Assert.ThrowsAsync<RuleViolation>(()=>s.SaveUnit(owner,u.Id,0,Settings(u,1)));
        await using var check=fixture.Db();Assert.Equal(0,(await check.Units.SingleAsync(x=>x.Id==u.Id)).Turnover);
        var receipt=await s.Submit(p.Id,new(u.Id,Day.AddDays(10),Day.AddDays(12),2,"Guest","+389 70 000 002",BookingService.Secret(),true));
        await s.SaveUnit(owner,u.Id,0,Settings(u) with{MinNights=3});
        await Assert.ThrowsAsync<RuleViolation>(()=>s.Confirm(owner,receipt.Id,0,100,true));
    }

    [Fact] public async Task BlockEditRemovalRetriesAndTenantChecksCannotBypassOwnership()
    {
        var(owner,p,u)=await fixture.Property();var(other,q,v)=await fixture.Property();await using var db=fixture.Db();var s=fixture.Service(db);
        var id=Guid.NewGuid();var a=await s.SaveBlock(owner,id,null,u.Id,Day,Day.AddDays(2),"Maintenance");
        Assert.Equal(a.Id,(await s.SaveBlock(owner,id,null,u.Id,Day,Day.AddDays(2),"Maintenance")).Id);
        await Assert.ThrowsAsync<RuleViolation>(()=>s.SaveBlock(other,id,1,u.Id,Day,Day.AddDays(2),"Forged"));
        await Assert.ThrowsAsync<RuleViolation>(()=>s.RemoveBlock(other,id,1));
        await Assert.ThrowsAsync<RuleViolation>(()=>s.ManualBooking(other,Guid.NewGuid(),Stay(u)));
        await s.SaveBlock(owner,id,1,u.Id,Day.AddDays(2),Day.AddDays(4),"Updated closure");
        await Assert.ThrowsAsync<RuleViolation>(()=>s.RemoveBlock(owner,id,1));await s.RemoveBlock(owner,id,2);await s.RemoveBlock(owner,id,2);
        await using var bypass=fixture.Db();bypass.Allocations.Add(new(){PropertyId=q.Id,UnitId=u.Id,Start=Day,End=Day.AddDays(1)});
        var e=await Assert.ThrowsAsync<DbUpdateException>(()=>bypass.SaveChangesAsync());Assert.Equal("23503",Assert.IsType<PostgresException>(e.InnerException).SqlState);
    }

    [Fact] public async Task DeclineIsTerminalAndPublicationPausesNewDecisionsWithoutDeletingBookings()
    {
        var(owner,p,u)=await fixture.Property();await using var db=fixture.Db();var s=fixture.Service(db);
        var receipt=await s.Submit(p.Id,new(u.Id,Day,Day.AddDays(2),2,"Guest","+389 70 000 002",BookingService.Secret(),true));
        await s.Decline(owner,receipt.Id,0,"Unavailable externally");await s.Decline(owner,receipt.Id,0,"Unavailable externally");
        Assert.Equal("Declined",(await s.Status(receipt.Token!))!.State);
        await Assert.ThrowsAsync<RuleViolation>(()=>s.Confirm(owner,receipt.Id,1,100,true));
        await s.UpdateProperty(owner,0,p.Name,p.Contact,p.Policies,false,false);
        Assert.False(await s.Available(u,Day,Day.AddDays(2),2));
        await s.ManualBooking(owner,Guid.NewGuid(),Stay(u)); // owner can maintain inventory while public requests are paused
        Assert.Single(await db.Reservations.Where(x=>x.PropertyId==p.Id).ToListAsync());
    }

    [Fact] public async Task MaintenanceExpiresRedactsAndNeverPurgesUpcomingReservations()
    {
        var(owner,p,u)=await fixture.Property();Receipt receipt;Guid upcoming;
        await using(var db=fixture.Db())
        {
            var s=fixture.Service(db);receipt=await s.Submit(p.Id,new(u.Id,Day,Day.AddDays(2),2,"Guest","+389 70 000 002",BookingService.Secret(),true));
            upcoming=(await s.ManualBooking(owner,Guid.NewGuid(),Stay(u,Day.AddDays(300)))).Id;
        }
        await using(var db=fixture.Db()) { var s=fixture.Service(db,new FrozenTime(receipt.Expires));await s.Maintain();Assert.Equal("Expired",(await db.Requests.SingleAsync(x=>x.Id==receipt.Id)).State); }
        await using(var db=fixture.Db())
        {
            var s=fixture.Service(db,new FrozenTime(receipt.Expires.AddDays(91)));await s.Maintain();
            var r=await db.Requests.SingleAsync(x=>x.Id==receipt.Id);Assert.True(r.Redacted);Assert.Empty(r.Name);Assert.Empty(r.Phone);Assert.Empty(r.ProtectedReceipt);Assert.Null(await s.Status(receipt.Token!));
            var booking=await db.Reservations.SingleAsync(x=>x.Id==upcoming);Assert.False(booking.Redacted);Assert.Equal("Confirmed",booking.State);
        }
    }

    [Fact] public async Task RetentionRedactsPastBookingsBeforeRemovingOldNonpersonalHistory()
    {
        var(owner,p,u)=await fixture.Property(); Guid id; Receipt receipt;
        await using(var db=fixture.Db())
        {
            var s=fixture.Service(db);receipt=await s.Submit(p.Id,new(u.Id,Day,Day.AddDays(2),2,"Guest","+389 70 000 002",BookingService.Secret(),true));
            id=(await s.Confirm(owner,receipt.Id,0,100,true)).Id;
        }
        await using(var db=fixture.Db())
        {
            await fixture.Service(db,new FrozenTime(DateTimeOffset.UtcNow.AddMonths(13))).Maintain();
            var r=await db.Reservations.SingleAsync(x=>x.Id==id);Assert.True(r.Redacted);Assert.Empty(r.Name);Assert.Empty(r.Phone);
            var q=await db.Requests.SingleAsync(x=>x.Id==receipt.Id);Assert.True(q.Redacted);Assert.Empty(q.Policies);
            Assert.Single(await db.Allocations.Where(x=>x.ReservationId==id).ToListAsync());
        }
        await using(var db=fixture.Db())
        {
            await fixture.Service(db,new FrozenTime(DateTimeOffset.UtcNow.AddMonths(25))).Maintain();
            await fixture.Service(db,new FrozenTime(DateTimeOffset.UtcNow.AddMonths(25))).Maintain();
            Assert.False(await db.Reservations.AnyAsync(x=>x.Id==id));Assert.False(await db.Requests.AnyAsync(x=>x.Id==receipt.Id));Assert.False(await db.Allocations.AnyAsync(x=>x.ReservationId==id));
        }
    }

    [Fact] public async Task PolicyChangeRejectsAnOldGuestFormAndTurnoverGapOccupiesAnExtraNight()
    {
        var(owner,p,u)=await fixture.Property();await using var db=fixture.Db();var s=fixture.Service(db);
        await s.UpdateProperty(owner,0,p.Name,p.Contact,"New cancellation policy: call the property before any changes.",true,true);
        await Assert.ThrowsAsync<RuleViolation>(()=>s.Submit(p.Id,new(u.Id,Day,Day.AddDays(2),2,"Guest","+389 70 000 002",BookingService.Secret(),true,"1")));
        var updated=await s.SaveUnit(owner,u.Id,0,Settings(u,1));
        var r=await s.ManualBooking(owner,Guid.NewGuid(),Stay(updated));
        Assert.False(await s.Available(updated,Day.AddDays(2),Day.AddDays(4),2));Assert.True(await s.Available(updated,Day.AddDays(3),Day.AddDays(5),2));
        await s.CancelReservation(owner,r.Id,0,true);Assert.True(await s.Available(updated,Day.AddDays(2),Day.AddDays(4),2));
    }

    [Fact] public async Task ConcurrentDateEditAndBlockAllowExactlyOneChange()
    {
        var(owner,p,u)=await fixture.Property();Guid id;
        await using(var db=fixture.Db())id=(await fixture.Service(db).ManualBooking(owner,Guid.NewGuid(),Stay(u))).Id;
        async Task<bool> Edit(){await using var db=fixture.Db();try{await fixture.Service(db).EditReservation(owner,id,0,Stay(u,Day.AddDays(5)));return true;}catch(RuleViolation){return false;}}
        async Task<bool> Block(){await using var db=fixture.Db();try{await fixture.Service(db).SaveBlock(owner,Guid.NewGuid(),null,u.Id,Day.AddDays(5),Day.AddDays(7),"Closure");return true;}catch(RuleViolation){return false;}}
        Assert.Single(await Task.WhenAll(Edit(),Block()),x=>x);
        await using var check=fixture.Db();var r=await check.Reservations.SingleAsync(x=>x.Id==id);
        var a=await check.Allocations.SingleAsync(x=>x.ReservationId==id);Assert.Equal(r.Arrival,a.Start);Assert.Equal(r.Departure,a.End);
    }

    [Fact] public async Task TransactionLeasePreventsDuplicateMaintenanceAndResumesAfterRelease()
    {
        var(owner,p,u)=await fixture.Property();Receipt receipt;
        await using(var db=fixture.Db())receipt=await fixture.Service(db).Submit(p.Id,new(u.Id,Day,Day.AddDays(2),2,"Guest","+389 70 000 002",BookingService.Secret(),true));
        await using var connection=new NpgsqlConnection(fixture.Connection);await connection.OpenAsync();await using var lease=await connection.BeginTransactionAsync();
        await using(var cmd=new NpgsqlCommand("SELECT pg_advisory_xact_lock(741038221)",connection,lease))await cmd.ExecuteNonQueryAsync();
        await using(var db=fixture.Db()){Assert.Equal(0,await fixture.Service(db,new FrozenTime(receipt.Expires)).Maintain());Assert.Equal("Pending",(await db.Requests.SingleAsync(x=>x.Id==receipt.Id)).State);}
        await lease.RollbackAsync();
        await using(var db=fixture.Db()){await fixture.Service(db,new FrozenTime(receipt.Expires)).Maintain();Assert.Equal("Expired",(await db.Requests.SingleAsync(x=>x.Id==receipt.Id)).State);}
    }
}

public sealed class ExportTests
{
    [Theory][InlineData("=HYPERLINK(\"evil\")")][InlineData("  @SUM(A1)")][InlineData("+389 70 123")][InlineData("\t=1+1")][InlineData("\r\n-1")]
    public void CsvNeutralizesSpreadsheetFormulasAndQuotesValues(string input)
    { var cell=CsvExport.Cell(input);Assert.StartsWith("\"'",cell);Assert.EndsWith("\"",cell); }
}
