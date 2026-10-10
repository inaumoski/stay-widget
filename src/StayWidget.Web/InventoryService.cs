using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using StayWidget.Domain;
using StayWidget.Persistence;

namespace StayWidget.Web;

public sealed record UnitSettings(string Name, int Capacity, DateOnly OpenFrom, DateOnly OpenTo,
    int MinNights, int MaxNights, int Turnover, bool Enabled);
public sealed record ManualStay(Guid UnitId, DateOnly Arrival, DateOnly Departure, int Guests,
    string Name, string Phone, decimal Amount, bool Agreed);

public sealed partial class BookingService
{
    private void Record(Guid property, Guid? actor, Guid entity, string action, string details = "") =>
        db.Activity.Add(new ActivityRecord { PropertyId = property, ActorId = actor, EntityId = entity,
            Action = action, Details = details, At = clock.GetUtcNow() });

    // All mutations serialize on this property row, including the maintenance worker.
    private async Task<Property> Lock(Guid owner)
    {
        db.ChangeTracker.Clear();
        return await db.Properties.FromSqlInterpolated($"SELECT * FROM \"Properties\" WHERE \"OwnerId\"={owner} FOR UPDATE")
            .SingleOrDefaultAsync() ?? throw new RuleViolation("Property not found.");
    }

    private async Task Save()
    {
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { throw new RuleViolation("This item changed. Refresh and review it before retrying."); }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: "23P01" or "23505" })
        { throw new RuleViolation("These dates overlap another booking or block, or this name is already used. Refresh and review the calendar."); }
    }

    public async Task UpdateProperty(Guid owner, int revision, string name, string contact, string policies, bool published, bool understood)
    {
        name = name.Trim(); contact = contact.Trim(); policies = policies.Trim();
        if (name.Length is < 1 or > 120 || contact.Length is < 3 or > 200 || policies.Length is < 10 or > 4000 || published && !understood)
            throw new RuleViolation("Check the name, contact and policies, and acknowledge manual inventory and no-email responsibilities before publishing.");
        await using var tx = await db.Database.BeginTransactionAsync(); var p = await Lock(owner);
        if (p.Revision != revision) throw new RuleViolation("Property settings changed. Refresh before saving.");
        if (p.Policies != policies) p.TermsVersion = Guid.NewGuid().ToString("N");
        p.Name = name; p.Contact = contact; p.Policies = policies; p.Published = published; p.Revision++;
        Record(p.Id, owner, p.Id, published ? "Property published/settings updated" : "New requests paused");
        await Save(); await tx.CommitAsync();
    }

    public async Task<Unit> SaveUnit(Guid owner, Guid id, int? revision, UnitSettings input)
    {
        var name = input.Name.Trim();
        if (id == Guid.Empty || name.Length is < 1 or > 100 || input.Capacity is < 1 or > 20 || input.OpenFrom >= input.OpenTo ||
            input.MinNights < 1 || input.MaxNights > 28 || input.MaxNights < input.MinNights || input.Turnover is < 0 or > 1)
            throw new RuleViolation("Set a unique unit name, capacity 1–20, ordered open dates, stays 1–28 nights and turnover of 0 or 1 night.");
        await using var tx = await db.Database.BeginTransactionAsync(); var p = await Lock(owner);
        var u = await db.Units.SingleOrDefaultAsync(x => x.PropertyId == p.Id && x.Id == id);
        if (u == null)
        {
            if (revision.HasValue) throw new RuleViolation("Unit not found.");
            if (await db.Units.CountAsync(x => x.PropertyId == p.Id) >= ProductPlans.StoredUnitLimit) throw new RuleViolation("This preview stores at most five units, including closed drafts. Existing history is preserved; contact through Custom for larger requirements.");
            // New units are always draft; a second deliberate save opens them for requests.
            u = new Unit { Id = id, PropertyId = p.Id, Enabled = false }; db.Units.Add(u);
        }
        else if (!revision.HasValue)
        { throw new RuleViolation("This unit was already created. Review it before editing."); }
        else if (u.Revision != revision.Value) throw new RuleViolation("Unit settings changed. Refresh before saving.");
        if (revision.HasValue && input.Enabled && !u.Enabled && await db.Units.CountAsync(x => x.PropertyId == p.Id && x.Enabled) >= ProductPlans.ActiveLimit(p))
            throw new RuleViolation($"{p.Plan} permits {ProductPlans.ActiveLimit(p)} open units. Close an unused unit or choose free early-access Pro. Existing bookings remain intact.");
        if (await db.Units.AnyAsync(x => x.PropertyId == p.Id && x.Id != id && x.Name == name)) throw new RuleViolation("Use a unique unit name.");
        var today = StayRules.Today(clock.GetUtcNow(), p.Zone);
        var future = await db.Reservations.Where(x => x.PropertyId == p.Id && x.UnitId == id && x.State == "Confirmed" && x.Departure > today).ToListAsync();
        if (future.Any(x => x.Guests > input.Capacity)) throw new RuleViolation("Capacity would be below an existing booking's guest count. Resolve that booking first.");
        if (u.Turnover != input.Turnover)
        {
            var allocations = await db.Allocations.Where(x => x.PropertyId == p.Id && x.UnitId == id && x.Active).ToListAsync();
            var reservations = await db.Reservations.Where(x => x.PropertyId == p.Id && x.UnitId == id && x.State == "Confirmed").ToDictionaryAsync(x => x.Id);
            foreach (var a in allocations.Where(x => x.ReservationId.HasValue && x.End > today))
                a.End = reservations[a.ReservationId!.Value].Departure.AddDays(input.Turnover);
            var ordered = allocations.OrderBy(x => x.Start).ToList();
            if (ordered.Zip(ordered.Skip(1)).Any(pair => pair.First.End > pair.Second.Start))
                throw new RuleViolation("This turnover buffer conflicts with an existing booking or block. Resolve the conflict first.");
        }
        u.Name = name; u.Capacity = input.Capacity; u.OpenFrom = input.OpenFrom; u.OpenTo = input.OpenTo;
        u.MinNights = input.MinNights; u.MaxNights = input.MaxNights; u.Turnover = input.Turnover;
        u.Enabled = revision.HasValue && input.Enabled; u.Revision++;
        Record(p.Id, owner, u.Id, "Unit settings saved", "Existing bookings are preserved; new season/stay rules apply to new decisions.");
        await Save(); await tx.CommitAsync(); return u;
    }

    private static void CheckStay(ManualStay input)
    {
        if (!input.Agreed || input.Name.Trim().Length is < 1 or > 120 || input.Phone.Trim().Length is < 5 or > 40 ||
            input.Amount < 0 || input.Amount > 9999999999m || decimal.Round(input.Amount, 2) != input.Amount)
            throw new RuleViolation("Record the guest's name and phone, agreed total with up to two decimals, and acknowledge agreement and outside inventory checks.");
    }

    private async Task CheckInventory(Property p, Unit u, DateOnly arrival, DateOnly departure, int guests, Guid? excluding = null)
    {
        // Owner entries can start today, including a stay already in progress; never future-date an existing guest.
        var today = StayRules.Today(clock.GetUtcNow(), p.Zone);
        var earliest = excluding.HasValue ? await db.Reservations.Where(x => x.Id == excluding.Value).Select(x => x.Arrival).SingleAsync() : today;
        if (arrival < today && arrival != earliest) throw new RuleViolation("A past arrival can only be preserved from the original reservation.");
        StayRules.Validate(arrival, departure, guests, u.Capacity, arrival < today ? arrival.AddDays(-1) : today.AddDays(-1), u.OpenFrom, u.OpenTo, u.MinNights, u.MaxNights);
        if (await db.Allocations.AnyAsync(x => x.PropertyId == p.Id && x.UnitId == u.Id && x.Active &&
            (!excluding.HasValue || x.ReservationId != excluding) && x.Start < departure.AddDays(u.Turnover) && arrival < x.End))
            throw new RuleViolation("These dates overlap an existing booking or block. Review the calendar.");
    }

    public async Task<Reservation> ManualBooking(Guid owner, Guid id, ManualStay input)
    {
        CheckStay(input); if (id == Guid.Empty) throw new RuleViolation("Invalid booking identifier.");
        var digest = Hash(JsonSerializer.Serialize(input));
        await using var tx = await db.Database.BeginTransactionAsync(); var p = await Lock(owner);
        var existing = await db.Reservations.SingleOrDefaultAsync(x => x.Id == id && x.PropertyId == p.Id);
        if (existing != null)
        {
            if (existing.CommandHash != digest) throw new RuleViolation("This booking was already saved with different details. Review it before editing.");
            return existing;
        }
        var u = await db.Units.SingleOrDefaultAsync(x => x.Id == input.UnitId && x.PropertyId == p.Id) ?? throw new RuleViolation("Unit not found.");
        await CheckInventory(p, u, input.Arrival, input.Departure, input.Guests);
        var r = new Reservation { Id = id, PropertyId = p.Id, UnitId = u.Id, Arrival = input.Arrival, Departure = input.Departure,
            Guests = input.Guests, Name = input.Name.Trim(), Phone = input.Phone.Trim(), Amount = input.Amount, Currency = p.Currency,
            TermsVersion = p.TermsVersion, Confirmed = clock.GetUtcNow(), CommandHash = digest };
        db.Reservations.Add(r); db.Allocations.Add(new InventoryAllocation { PropertyId = p.Id, UnitId = u.Id, ReservationId = id,
            Start = r.Arrival, End = r.Departure.AddDays(u.Turnover), Created = clock.GetUtcNow() });
        Record(p.Id, owner, id, "Manual booking added", $"{r.Arrival} to {r.Departure}");
        await Save(); await tx.CommitAsync(); return r;
    }

    public async Task EditReservation(Guid owner, Guid id, int revision, ManualStay input)
    {
        CheckStay(input); await using var tx = await db.Database.BeginTransactionAsync(); var p = await Lock(owner);
        var r = await db.Reservations.SingleOrDefaultAsync(x => x.PropertyId == p.Id && x.Id == id) ?? throw new RuleViolation("Reservation not found.");
        if (r.Revision != revision || r.State != "Confirmed" || r.Redacted) throw new RuleViolation("This reservation changed or is closed. Refresh before editing.");
        if (input.UnitId != r.UnitId) throw new RuleViolation("Change dates within the original unit. Moving a booking between units is outside this release.");
        var u = await db.Units.SingleAsync(x => x.PropertyId == p.Id && x.Id == r.UnitId);
        // Season/rules may have changed without cancelling this stay; unchanged dates can be preserved.
        if (r.Arrival != input.Arrival || r.Departure != input.Departure || r.Guests != input.Guests)
            await CheckInventory(p, u, input.Arrival, input.Departure, input.Guests, id);
        var a = await db.Allocations.SingleAsync(x => x.PropertyId == p.Id && x.ReservationId == id);
        var before = $"{r.Arrival} to {r.Departure}";
        r.Arrival = input.Arrival; r.Departure = input.Departure; r.Guests = input.Guests;
        r.Name = input.Name.Trim(); r.Phone = input.Phone.Trim(); r.Amount = input.Amount; r.Revision++;
        a.Start = r.Arrival; a.End = r.Departure.AddDays(u.Turnover); a.Revision++;
        Record(p.Id, owner, id, "Reservation edited", $"{before} -> {r.Arrival} to {r.Departure}");
        await Save(); await tx.CommitAsync();
    }

    public async Task CancelReservation(Guid owner, Guid id, int revision, bool reviewed)
    {
        if (!reviewed) throw new RuleViolation("Review your cancellation obligations and acknowledge releasing these dates.");
        await using var tx = await db.Database.BeginTransactionAsync(); var p = await Lock(owner);
        var r = await db.Reservations.SingleOrDefaultAsync(x => x.PropertyId == p.Id && x.Id == id) ?? throw new RuleViolation("Reservation not found.");
        if (r.State == "Cancelled") return;
        if (r.Revision != revision) throw new RuleViolation("Reservation changed. Review its current dates before cancelling.");
        r.State = "Cancelled"; r.Cancelled = clock.GetUtcNow(); r.CancellationRequested = false; r.Revision++;
        var a = await db.Allocations.SingleAsync(x => x.PropertyId == p.Id && x.ReservationId == id); a.Active = false; a.Revision++;
        Record(p.Id, owner, id, "Reservation cancelled", $"{r.Arrival} to {r.Departure}; dates released");
        await Save(); await tx.CommitAsync();
    }

    public async Task Decline(Guid owner, Guid id, int revision, string reason)
    {
        reason = reason.Trim(); if (reason.Length is < 1 or > 300) throw new RuleViolation("Give a public reason of 1–300 characters. Do not include private guest details.");
        await using var tx = await db.Database.BeginTransactionAsync(); var p = await Lock(owner);
        var r = await db.Requests.SingleOrDefaultAsync(x => x.Id == id && x.PropertyId == p.Id) ?? throw new RuleViolation("Request not found.");
        if (r.State == "Declined" && r.PublicReason == reason) return;
        if (r.State != "Pending" || r.Revision != revision || clock.GetUtcNow() >= r.Expires) throw new RuleViolation("Request changed or expired. Refresh before deciding.");
        r.State = "Declined"; r.PublicReason = reason; r.Closed = clock.GetUtcNow(); r.Revision++;
        Record(p.Id, owner, id, "Request declined"); await Save(); await tx.CommitAsync();
    }

    public async Task<InventoryAllocation> SaveBlock(Guid owner, Guid id, int? revision, Guid unitId, DateOnly start, DateOnly end, string reason)
    {
        reason = reason.Trim();
        if (id == Guid.Empty || start >= end || end.DayNumber - start.DayNumber > 730 || reason.Length is < 1 or > 200)
            throw new RuleViolation("Enter ordered blocked dates (end excluded), up to 730 nights, and a private reason of 1–200 characters.");
        var digest = Hash(JsonSerializer.Serialize(new { unitId, start, end, reason }));
        await using var tx = await db.Database.BeginTransactionAsync(); var p = await Lock(owner);
        if (!await db.Units.AnyAsync(x => x.PropertyId == p.Id && x.Id == unitId)) throw new RuleViolation("Unit not found.");
        var a = await db.Allocations.SingleOrDefaultAsync(x => x.PropertyId == p.Id && x.Id == id);
        if (a != null && !revision.HasValue)
        { if (a.CommandHash != digest || a.ReservationId.HasValue) throw new RuleViolation("This block was already saved with different details."); return a; }
        if (a != null && (a.ReservationId.HasValue || !a.Active || a.Revision != revision)) throw new RuleViolation("Block changed or is inactive. Refresh before editing.");
        if (a == null && revision.HasValue) throw new RuleViolation("Block not found.");
        if (await db.Allocations.AnyAsync(x => x.PropertyId == p.Id && x.UnitId == unitId && x.Active && x.Id != id && x.Start < end && start < x.End))
            throw new RuleViolation("This block overlaps another booking or block. Merge/edit blocks or resolve the booking first.");
        if (a == null) { a = new InventoryAllocation { Id = id, PropertyId = p.Id, Created = clock.GetUtcNow(), CommandHash = digest }; db.Allocations.Add(a); }
        a.UnitId = unitId; a.Start = start; a.End = end; a.Reason = reason; a.Revision++;
        Record(p.Id, owner, id, "Manual block saved", $"{start} to {end}"); await Save(); await tx.CommitAsync(); return a;
    }

    public async Task RemoveBlock(Guid owner, Guid id, int revision)
    {
        await using var tx = await db.Database.BeginTransactionAsync(); var p = await Lock(owner);
        var a = await db.Allocations.SingleOrDefaultAsync(x => x.PropertyId == p.Id && x.Id == id && x.ReservationId == null) ?? throw new RuleViolation("Manual block not found.");
        if (!a.Active) return;
        if (a.Revision != revision) throw new RuleViolation("Block changed. Refresh before removing it.");
        a.Active = false; a.Reason = ""; a.Revision++;
        Record(p.Id, owner, id, "Manual block removed", $"{a.Start} to {a.End}"); await Save(); await tx.CommitAsync();
    }
}
