using Microsoft.EntityFrameworkCore;
using StayWidget.Domain;
using StayWidget.Persistence;
namespace StayWidget.Web;
public sealed record CalendarNight(DateOnly Date, bool Available);
public sealed record CalendarMonth(DateOnly Month, DateOnly FirstMonth, DateOnly LastMonth, List<CalendarNight> Nights);
public sealed record CalendarView(string Endpoint, string UnitId, int MinNights, string InitialMonth);
public static class AvailabilityCalendar
{
    public static DateOnly Month(DateOnly day) => new(day.Year,day.Month,1);
    public static CalendarMonth Build(Unit unit, DateOnly today, DateOnly month, int guests, IEnumerable<(DateOnly Start,DateOnly End)> occupied)
    {
        var first=Month(today);var last=Month(today.AddDays(365));
        if(month.Day!=1 || month<first || month>last || guests is <1 or >20) throw new RuleViolation("Choose a calendar month within the next 365 days and 1–20 guests.");
        var periods=occupied.ToArray();var nights=new List<CalendarNight>();
        for(var d=month;d<month.AddMonths(1);d=d.AddDays(1))
            nights.Add(new(d,unit.Enabled && guests<=unit.Capacity && d>today && d<=today.AddDays(365) && d>=unit.OpenFrom && d<unit.OpenTo && !periods.Any(p=>p.Start<=d && d<p.End)));
        return new(month,first,last,nights);
    }
    public static readonly Guid DemoCottageId = new("11111111-1111-4111-8111-111111111111");
    public static readonly Guid DemoApartmentId = new("22222222-2222-4222-8222-222222222222");
    public static Unit DemoUnit(DateOnly today,Guid? unitId=null)
    {
        var id=unitId??DemoCottageId;
        if(id!=DemoCottageId && id!=DemoApartmentId)throw new RuleViolation("Choose a sample accommodation.");
        return new(){Id=id,Name=id==DemoCottageId?"Garden Cottage":"Sunny Apartment",Capacity=id==DemoCottageId?2:4,OpenFrom=today.AddDays(1),OpenTo=today.AddDays(91)};
    }
    public static Unit[] DemoUnits(DateOnly today)=>[DemoUnit(today,DemoCottageId),DemoUnit(today,DemoApartmentId)];
    public static (DateOnly Start,DateOnly End) DemoBlock(DateOnly today,Guid? unitId=null)
    {
        var unit=DemoUnit(today,unitId);var offset=unit.Id==DemoCottageId?14:7;
        return(today.AddDays(offset),today.AddDays(offset+3));
    }
}
public sealed partial class BookingService
{
    public async Task<CalendarMonth?> Calendar(Guid propertyId, Guid unitId, DateOnly month, int guests)
    {
        var p=await db.Properties.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==propertyId && x.Published && !x.Suspended && x.ShowAvailabilityCalendar);
        if(p==null) return null;
        var unit=await db.Units.AsNoTracking().SingleOrDefaultAsync(x=>x.PropertyId==propertyId && x.Id==unitId && x.Enabled);if(unit==null)return null;
        var today=StayRules.Today(clock.GetUtcNow(),p.Zone);
        // Validate bounds before querying occupancy; expose nights only, never sources/reasons/guest data.
        AvailabilityCalendar.Build(unit,today,month,guests,[]);
        var end=month.AddMonths(1);
        var ranges=await db.Allocations.AsNoTracking().Where(x=>x.PropertyId==propertyId && x.UnitId==unitId && x.Active && x.Start<end && x.End>month).Select(x=>new{x.Start,x.End}).ToListAsync();
        return AvailabilityCalendar.Build(unit,today,month,guests,ranges.Select(x=>(x.Start,x.End)));
    }
}
