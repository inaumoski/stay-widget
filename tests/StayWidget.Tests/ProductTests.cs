using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using StayWidget.Domain;
using StayWidget.Persistence;
using StayWidget.Web;
using Xunit;
namespace StayWidget.Tests;

public sealed class ProductTests(TestDatabase fixture) : IClassFixture<TestDatabase>
{
    private static UnitSettings Settings(Unit u,bool enabled=true)=>new(u.Name,u.Capacity,u.OpenFrom,u.OpenTo,u.MinNights,u.MaxNights,u.Turnover,enabled);
    [Fact] public async Task FreeBlocksGrowthButPreservesInventoryAndProRestoresAccess()
    {
        var(owner,p,u)=await fixture.Property();await using var db=fixture.Db();var s=fixture.Service(db);
        var draft=await s.SaveUnit(owner,Guid.NewGuid(),null,Settings(u) with{Name="Second"});
        await s.SetPlan(owner,0,"Free",true,true);var current=await db.Properties.AsNoTracking().SingleAsync(x=>x.Id==p.Id);
        Assert.True(ProductPlans.ShowBranding(current));Assert.False(current.HideBranding);
        await Assert.ThrowsAsync<RuleViolation>(()=>s.SaveUnit(owner,draft.Id,draft.Revision,Settings(draft)));
        var input=new ManualStay(u.Id,u.OpenFrom.AddDays(2),u.OpenFrom.AddDays(4),2,"Existing Guest","000 000 000",90,true);
        var booking=await s.ManualBooking(owner,Guid.NewGuid(),input);
        await s.SetPlan(owner,current.Revision,"Pro",true,true);
        await s.SaveUnit(owner,draft.Id,draft.Revision,Settings(draft));
        current=await db.Properties.AsNoTracking().SingleAsync(x=>x.Id==p.Id);Assert.False(ProductPlans.ShowBranding(current));
        await s.SetPlan(owner,current.Revision,"Free",false,true);
        Assert.Equal(2,await db.Units.CountAsync(x=>x.PropertyId==p.Id&&x.Enabled));Assert.True(await db.Allocations.AnyAsync(x=>x.ReservationId==booking.Id&&x.Active));
        await s.SaveUnit(owner,draft.Id,draft.Revision+1,Settings(draft) with{Name="Updated existing"});
        await Assert.ThrowsAsync<RuleViolation>(()=>s.SetPlan(Guid.NewGuid(),0,"Pro",true,true));
    }
    [Fact] public async Task ConcurrentFreeReopeningHasOneWinner()
    {
        var(owner,p,u)=await fixture.Property();Unit a,b;
        await using(var db=fixture.Db()){var s=fixture.Service(db);await s.SaveUnit(owner,u.Id,u.Revision,Settings(u,false));a=await s.SaveUnit(owner,Guid.NewGuid(),null,Settings(u)with{Name="A"});b=await s.SaveUnit(owner,Guid.NewGuid(),null,Settings(u)with{Name="B"});await s.SetPlan(owner,0,"Free",false,true);}
        async Task<bool> Open(Unit unit){await using var db=fixture.Db();try{await fixture.Service(db).SaveUnit(owner,unit.Id,unit.Revision,Settings(unit));return true;}catch(RuleViolation){return false;}}
        Assert.Single(await Task.WhenAll(Open(a),Open(b)),x=>x);await using var check=fixture.Db();Assert.Equal(1,await check.Units.CountAsync(x=>x.PropertyId==p.Id&&x.Enabled));
    }
    [Fact] public async Task PromotionBoundaryNeverChangesExistingAccess()
    {
        var(owner,p,_)=await fixture.Property();await using var db=fixture.Db();var after=ProductPlans.PromotionEnd;
        Assert.Contains("100%",ProductPlans.Offer(after.AddTicks(-1)));Assert.Contains("continued free",ProductPlans.Offer(after));
        await fixture.Service(db,new FrozenTime(after)).SetPlan(owner,0,"Pro",true,true);Assert.Equal("Pro",(await db.Properties.SingleAsync(x=>x.Id==p.Id)).Plan);
    }
    [Fact] public async Task FeedbackConcurrentRetriesPreserveManagedPrivateFieldsAndCustomGrantsNothing()
    {
        var(owner,p,_)=await fixture.Property();var key=Guid.NewGuid();
        async Task Submit(){await using var db=fixture.Db();await new FeedbackService(db,TimeProvider.System).Submit(owner,key,"Need a calendar improvement.","Custom","deliberate@example.test",true);}
        await Task.WhenAll(Submit(),Submit());await using var check=fixture.Db();var row=await check.Feedback.SingleAsync(x=>x.SubmitterUserId==owner);
        Assert.Equal("preview-2026-10-10",row.NoticeVersion);Assert.Null(row.Response);Assert.Equal("Pro",(await check.Properties.SingleAsync(x=>x.Id==p.Id)).Plan);
        row.Comment="PRIVATE_COMMENT";row.Response="PRIVATE_RESPONSE";row.Status="Reviewed";row.Reviewed=DateTimeOffset.UtcNow;row.Revision++;await check.SaveChangesAsync();
        await Submit();await check.Entry(row).ReloadAsync();Assert.Equal("PRIVATE_COMMENT",row.Comment);Assert.Equal("Reviewed",row.Status);
        await Assert.ThrowsAsync<RuleViolation>(()=>new FeedbackService(check,TimeProvider.System).Submit(owner,key,"Changed requirements.","Custom","",true));
    }
    [Fact] public async Task FeedbackLimitsRetentionAndAccountCascadeAreBounded()
    {
        Guid user;await using var db=fixture.Db();user=await fixture.Service(db).User("urn:feedback",Guid.NewGuid().ToString(),"Feedback only");
        var service=new FeedbackService(db,TimeProvider.System);
        for(int i=0;i<5;i++)await service.Submit(user,Guid.NewGuid(),"Useful request number "+i,"Feedback","",true);
        await Assert.ThrowsAsync<RuleViolation>(()=>service.Submit(user,Guid.NewGuid(),"Another useful request","Feedback","",true));
        await Assert.ThrowsAsync<RuleViolation>(()=>service.Submit(user,Guid.NewGuid(),"Useful but no acknowledgement","Feedback","",false));
        var rows=await db.Feedback.Where(x=>x.SubmitterUserId==user).ToListAsync();var old=DateTimeOffset.UtcNow.AddMonths(-13);
        foreach(var r in rows){r.Created=old;r.Updated=old;}
        rows[0].Status="Planned";rows[0].Reviewed=old;await db.SaveChangesAsync();
        await service.Cleanup(default);Assert.Single(await db.Feedback.Where(x=>x.SubmitterUserId==user).ToListAsync());
        await db.Users.Where(x=>x.Id==user).ExecuteDeleteAsync();Assert.False(await db.Feedback.AnyAsync(x=>x.SubmitterUserId==user));
    }
    [Fact] public async Task FeedbackStateConstraintAndRevisionRejectUnsafeManualWrites()
    {
        await using var db=fixture.Db();var user=await fixture.Service(db).User("urn:feedback",Guid.NewGuid().ToString(),"Feedback only");
        await new FeedbackService(db,TimeProvider.System).Submit(user,Guid.NewGuid(),"Please improve calendar navigation","Feedback","",true);
        var row=await db.Feedback.SingleAsync(x=>x.SubmitterUserId==user);row.Status="Closed";row.Closed=DateTimeOffset.UtcNow;row.Reviewed=DateTimeOffset.UtcNow;
        await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());db.ChangeTracker.Clear();
        var changed=await db.Feedback.Where(x=>x.Id==row.Id&&x.Revision==9).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Comment,"stale"));Assert.Equal(0,changed);
    }
    [Fact] public async Task AggregateCountersHonorChoicesCatalogAndRetentionWithoutIdentifiers()
    {
        var cfg=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"StayWidget:AggregateMetrics","true"}}).Build();
        var context=new HttpContextAccessor{HttpContext=new DefaultHttpContext()};context.HttpContext.Request.Headers.UserAgent="Synthetic browser";
        var metrics=new AggregateMetrics(cfg,context,TimeProvider.System);
        metrics.Record("page-home");metrics.Record("/GuestStatus#PRIVATE");context.HttpContext.Request.Headers["DNT"]="1";metrics.Record("page-home");context.HttpContext.Request.Headers.Remove("DNT");context.HttpContext.Request.Headers["Sec-GPC"]="1";metrics.Record("page-home");context.HttpContext.Request.Headers.Remove("Sec-GPC");
        metrics.Record("attribution-product");await using var db=fixture.Db();await metrics.Flush(db);var today=DateOnly.FromDateTime(DateTime.UtcNow);
        Assert.Equal(1,(await db.Metrics.SingleAsync(x=>x.Day==today&&x.Event=="page-home")).Count);Assert.DoesNotContain(await db.Metrics.ToListAsync(),x=>x.Event.Contains("PRIVATE"));
        db.Metrics.Add(new DailyMetric{Day=today.AddDays(-91),Event="page-home",Count=3});await db.SaveChangesAsync();await metrics.Flush(db);Assert.False(await db.Metrics.AnyAsync(x=>x.Day<today.AddDays(-90)));
        cfg["StayWidget:AggregateMetrics"]="false";metrics.Record("page-home");await metrics.Flush(db);Assert.Equal(1,(await db.Metrics.SingleAsync(x=>x.Day==today&&x.Event=="page-home")).Count);
    }
    [Fact] public async Task InstallationAcknowledgementIsIdempotentAndTenantScoped()
    {
        var(owner,p,_)=await fixture.Property();await using var db=fixture.Db();var s=fixture.Service(db);
        await Assert.ThrowsAsync<RuleViolation>(()=>s.AcknowledgeInstallation(owner,0,false));await s.AcknowledgeInstallation(owner,0,true);await s.AcknowledgeInstallation(owner,0,true);
        Assert.Single(await db.Activity.Where(x=>x.PropertyId==p.Id&&x.Action=="Installation instructions acknowledged").ToListAsync());await Assert.ThrowsAsync<RuleViolation>(()=>s.AcknowledgeInstallation(Guid.NewGuid(),0,true));
    }
    [Fact] public async Task RequestAggregateCountsOnlyCommittedNewRecordsNotRetriesOrFailures()
    {
        var(_,p,u)=await fixture.Property();await using var db=fixture.Db();
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"StayWidget:AggregateMetrics","true"}}).Build();
        var context=new HttpContextAccessor{HttpContext=new DefaultHttpContext()};context.HttpContext.Request.Headers.UserAgent="Synthetic browser";
        var metrics=new AggregateMetrics(config,context,TimeProvider.System);var s=new BookingService(db,TimeProvider.System,fixture.Protection,null,metrics);
        var input=new GuestInput(u.Id,u.OpenFrom.AddDays(10),u.OpenFrom.AddDays(12),2,"Synthetic guest","000 000 000",BookingService.Secret(),true);
        await s.Submit(p.Id,input);await s.Submit(p.Id,input);await Assert.ThrowsAsync<RuleViolation>(()=>s.Submit(p.Id,input with{Key=BookingService.Secret(),Agree=false}));
        await metrics.Flush(db);Assert.Equal(1,(await db.Metrics.SingleAsync(x=>x.Event=="request-submitted")).Count);
    }
    [Fact] public async Task RetentionRechecksReviewedStateUnderTheSameUserLock()
    {
        await using var db=fixture.Db();var user=await fixture.Service(db).User("urn:retention",Guid.NewGuid().ToString(),"Synthetic user");
        var service=new FeedbackService(db,TimeProvider.System);await service.Submit(user,Guid.NewGuid(),"Please improve the date selector","Feedback","",true);
        var row=await db.Feedback.SingleAsync(x=>x.SubmitterUserId==user);row.Created=DateTimeOffset.UtcNow.AddMonths(-13);await db.SaveChangesAsync();
        await using var tx=await db.Database.BeginTransactionAsync();await db.Users.FromSqlInterpolated($"SELECT * FROM \"Users\" WHERE \"Id\"={user} FOR UPDATE").SingleAsync();
        await using var cleanupDb=fixture.Db();var cleanup=new FeedbackService(cleanupDb,TimeProvider.System).Cleanup(default);
        row.Status="Reviewed";row.Reviewed=DateTimeOffset.UtcNow;row.Updated=DateTimeOffset.UtcNow;row.Revision++;await db.SaveChangesAsync();await tx.CommitAsync();await cleanup;
        Assert.True(await db.Feedback.AnyAsync(x=>x.Id==row.Id&&x.Status=="Reviewed"));
    }
}
