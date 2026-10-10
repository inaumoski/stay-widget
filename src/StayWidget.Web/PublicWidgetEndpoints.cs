using Microsoft.EntityFrameworkCore;
using StayWidget.Domain;
using StayWidget.Persistence;
namespace StayWidget.Web;

public sealed record PublicUnit(Guid Id, string Name, int Capacity, int MinNights, int MaxNights);
public sealed record PublicWidget(Guid Id, string Name, string Contact, string Policies, string Currency, string AccentColor, List<PublicUnit> Units, bool ShowAvailabilityCalendar);
public sealed record AvailabilityQuery(DateOnly Arrival, DateOnly Departure, int Guests);

public static class PublicWidgetEndpoints
{
    public static void MapPublicWidget(this WebApplication app)
    {
        app.MapGet("/api/widget/{id:guid}/calendar",async(Guid id,Guid unitId,DateOnly month,int guests,BookingService service)=>
        {
            try{var calendar=await service.Calendar(id,unitId,month,guests);return calendar==null?Results.NotFound():Results.Ok(calendar);}
            catch(RuleViolation e){return Results.BadRequest(new{message=e.Message});}
        }).RequireRateLimiting("guest");
        app.MapGet("/api/demo/calendar",(DateOnly month,int guests,TimeProvider clock,Guid? unitId=null)=>
        {
            try{var today=DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);return Results.Ok(AvailabilityCalendar.Build(AvailabilityCalendar.DemoUnit(today,unitId),today,month,guests,[AvailabilityCalendar.DemoBlock(today,unitId)]));}
            catch(RuleViolation e){return Results.BadRequest(new{message=e.Message});}
        }).RequireRateLimiting("guest");
        app.MapGet("/api/widget/{id:guid}/configuration", async (Guid id, StayDb db) =>
        {
            var p = await db.Properties.AsNoTracking().Where(x => x.Id == id && x.Published && !x.Suspended)
                .Select(x => new { x.Id, x.Name, x.Contact, x.Policies, x.Currency, x.AccentColor, x.ShowAvailabilityCalendar }).SingleOrDefaultAsync();
            if (p == null) return Results.NotFound();
            var units = await db.Units.AsNoTracking().Where(x => x.PropertyId == id && x.Enabled).OrderBy(x => x.Name)
                .Select(x => new PublicUnit(x.Id, x.Name, x.Capacity, x.MinNights, x.MaxNights)).ToListAsync();
            return Results.Ok(new PublicWidget(p.Id, p.Name, p.Contact, p.Policies, p.Currency, p.AccentColor, units, p.ShowAvailabilityCalendar));
        }).RequireRateLimiting("guest");
        app.MapPost("/api/widget/{id:guid}/availability", async (Guid id, AvailabilityQuery input, StayDb db, BookingService service, TimeProvider clock) =>
        {
            var p = await db.Properties.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.Published && !x.Suspended);
            if (p == null) return Results.NotFound();
            try
            {
                var today = StayRules.Today(clock.GetUtcNow(), p.Zone);
                StayRules.Validate(input.Arrival, input.Departure, input.Guests, 20, today, today, today.AddDays(394));
                var units = await db.Units.AsNoTracking().Where(x => x.PropertyId == id && x.Enabled).OrderBy(x => x.Name).ToListAsync();
                var options = new List<object>();
                foreach (var u in units)
                {
                    bool available;
                    try { available = await service.Available(u, input.Arrival, input.Departure, input.Guests); }
                    catch (RuleViolation) { available = false; }
                    options.Add(new { u.Id, u.Name, u.Capacity, Available = available });
                }
                return Results.Ok(new { Units = options, Message = "Available dates may be requested; they are not held and outside inventory must be checked by the property." });
            }
            catch (RuleViolation e) { return Results.BadRequest(new { message = e.Message }); }
        }).RequireRateLimiting("guest");
    }
}
