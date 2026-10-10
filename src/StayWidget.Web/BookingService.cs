using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StayWidget.Domain;
using StayWidget.Persistence;
namespace StayWidget.Web;

public sealed record GuestInput(Guid UnitId, DateOnly Arrival, DateOnly Departure, int Guests, string Name, string Phone, string Key, bool Agree, string? TermsVersion = null, string? Network = null);
public sealed record Receipt(Guid Id, string? Token, DateTimeOffset Expires, string? ActionToken = null);
public sealed record GuestStatus(string State, string Property, string Unit, DateOnly Arrival, DateOnly Departure, int Guests, DateTimeOffset Expires, string Contact, string Reason, decimal? Amount, string? Currency, bool CanWithdraw = false, bool CanRequestCancellation = false, bool CancellationRequested = false);
public sealed partial class BookingService(StayDb db, TimeProvider clock, IDataProtectionProvider protection, SubmissionGuard? submissions = null, AggregateMetrics? metrics = null)
{
    private readonly IDataProtector protector = protection.CreateProtector("StayWidget.Receipt.v1");
    private sealed record Tokens(string View, string Action);
    public static string Hash(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));
    public static string Secret() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public async Task<Guid> User(string issuer, string subject, string name)
    {
        if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(subject)) throw new RuleViolation("Invalid sign-in identity.");
        var id = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"Users\" (\"Id\",\"Issuer\",\"Subject\",\"Name\") VALUES ({id},{issuer},{subject},{(name.Length > 120 ? name[..120] : name)}) ON CONFLICT (\"Issuer\",\"Subject\") DO NOTHING");
        return await db.Users.Where(x => x.Issuer == issuer && x.Subject == subject).Select(x => x.Id).SingleAsync();
    }
    public async Task<Property> Setup(Guid owner, string name, string contact, string zone, string policies, string unitName, int capacity)
    {
        name = name.Trim(); contact = contact.Trim(); policies = policies.Trim(); unitName = unitName.Trim();
        if (name.Length is < 1 or > 120 || contact.Length is < 3 or > 200 || policies.Length is < 10 or > 4000 || unitName.Length is < 1 or > 100 || capacity is < 1 or > 20) throw new RuleViolation("Provide a property name, contact, policies, unit name and capacity of 1–20.");
        try { _ = TimeZoneInfo.FindSystemTimeZoneById(zone); } catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException) { throw new RuleViolation("Choose a valid property time zone."); }
        await using var tx = await db.Database.BeginTransactionAsync();
        _ = await db.Users.FromSqlInterpolated($"SELECT * FROM \"Users\" WHERE \"Id\"={owner} FOR UPDATE").SingleAsync();
        if (await db.Properties.AnyAsync(x => x.OwnerId == owner)) throw new RuleViolation("This account already has its property.");
        var p = new Property { OwnerId = owner, Name = name, Contact = contact, Zone = zone, Policies = policies };
        var today = StayRules.Today(clock.GetUtcNow(), zone);
        db.Properties.Add(p); db.Units.Add(new Unit { PropertyId = p.Id, Name = unitName, Capacity = capacity, OpenFrom = today.AddDays(1), OpenTo = today.AddDays(394) });
        await db.SaveChangesAsync(); await tx.CommitAsync(); return p;
    }
    public async Task<bool> Available(Unit unit, DateOnly arrival, DateOnly departure, int guests)
    {
        var p = await db.Properties.SingleAsync(x => x.Id == unit.PropertyId);
        StayRules.Validate(arrival, departure, guests, unit.Capacity, StayRules.Today(clock.GetUtcNow(), p.Zone), unit.OpenFrom, unit.OpenTo, unit.MinNights, unit.MaxNights);
        return p.Published && !p.Suspended && unit.Enabled && !await db.Allocations.AnyAsync(x => x.UnitId == unit.Id && x.Active && x.Start < departure.AddDays(unit.Turnover) && arrival < x.End);
    }
    public async Task<Receipt> Submit(Guid propertyId, GuestInput input)
    {
        var name = input.Name.Trim(); var phone = input.Phone.Trim();
        if (!input.Agree || name.Length is < 1 or > 120 || phone.Length is < 5 or > 40 || input.Key.Length != 64 || !input.Key.All(Uri.IsHexDigit)) throw new RuleViolation("Enter your name and phone, review the policies and try again.");
        var key = Hash(input.Key); var digest = Hash(JsonSerializer.Serialize(new { input.UnitId, input.Arrival, input.Departure, input.Guests, name, phone, input.Agree }));
        db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync();
        var p = await db.Properties.FromSqlInterpolated($"SELECT * FROM \"Properties\" WHERE \"Id\"={propertyId} FOR UPDATE").SingleOrDefaultAsync() ?? throw new RuleViolation("Property is unavailable.");
        var existing = await db.Requests.SingleOrDefaultAsync(x => x.PropertyId == p.Id && x.KeyHash == key);
        if (existing != null)
        {
            if (existing.PayloadHash != digest) throw new RuleViolation("This submission key has already been used for different details. Start a new request.");
            if (existing.CapabilitiesRevoked || p.Suspended || clock.GetUtcNow() >= existing.Created.AddHours(24) || existing.ProtectedReceipt.Length == 0)
                return new(existing.Id, null, existing.Expires);
            var cached = protector.Unprotect(existing.ProtectedReceipt);
            if (cached.StartsWith('{')) { var tokens = JsonSerializer.Deserialize<Tokens>(cached)!; return new(existing.Id, tokens.View, existing.Expires, tokens.Action); }
            return new(existing.Id, cached, existing.Expires); // Pre-M3 links remain view-only.
        }
        if (input.TermsVersion != null && input.TermsVersion != p.TermsVersion) throw new RuleViolation("Property policies changed. Reload the request page, review them and submit again.");
        var unit = await db.Units.SingleOrDefaultAsync(x => x.Id == input.UnitId && x.PropertyId == p.Id) ?? throw new RuleViolation("Choose an available unit.");
        if (!await Available(unit, input.Arrival, input.Departure, input.Guests)) throw new RuleViolation("These dates are no longer available. Choose other dates.");
        if (input.Network != null) submissions?.Acquire(input.Network);
        var day = new DateTimeOffset(clock.GetUtcNow().UtcDateTime.Date, TimeSpan.Zero);
        if (await db.Requests.CountAsync(x => x.PropertyId == p.Id && x.Created >= day && x.Created < day.AddDays(1)) >= 100)
            throw new SubmissionLimited((int)(day.AddDays(1) - clock.GetUtcNow()).TotalSeconds);
        // PostgreSQL timestamps retain microseconds; return the same deadline on first submit and retries.
        var instant = clock.GetUtcNow(); var now = new DateTimeOffset(instant.Ticks - instant.Ticks % 10, instant.Offset); var token = Secret(); var actionToken = Secret();
        var request = new ReservationRequest { PropertyId = p.Id, UnitId = unit.Id, Arrival = input.Arrival, Departure = input.Departure, Guests = input.Guests, Name = name, Phone = phone, Created = now, Expires = StayRules.Deadline(now, input.Arrival, p.Zone), Policies = p.Policies, TermsVersion = p.TermsVersion, KeyHash = key, PayloadHash = digest, TokenHash = Hash(token), ActionHash = Hash(actionToken), ProtectedReceipt = protector.Protect(JsonSerializer.Serialize(new Tokens(token, actionToken))) };
        db.Requests.Add(request); await db.SaveChangesAsync(); await tx.CommitAsync(); metrics?.Record("request-submitted"); return new(request.Id, token, request.Expires, actionToken);
    }
    public async Task<Reservation> Confirm(Guid owner, Guid requestId, int revision, decimal amount, bool agreed)
    {
        if (!agreed || amount < 0 || amount > 9999999999m || decimal.Round(amount, 2) != amount) throw new RuleViolation("Confirm external agreement and enter a valid EUR amount with at most two decimal places.");
        db.ChangeTracker.Clear();
        var propertyId = await db.Requests.Where(x => x.Id == requestId).Select(x => (Guid?)x.PropertyId).SingleOrDefaultAsync() ?? throw new RuleViolation("Request not found.");
        await using var tx = await db.Database.BeginTransactionAsync();
        var p = await db.Properties.FromSqlInterpolated($"SELECT * FROM \"Properties\" WHERE \"Id\"={propertyId} AND \"OwnerId\"={owner} FOR UPDATE").SingleOrDefaultAsync() ?? throw new RuleViolation("Request not found.");
        var r = await db.Requests.SingleAsync(x => x.Id == requestId && x.PropertyId == p.Id);
        if (r.State == "Accepted")
        {
            var booked = await db.Reservations.SingleAsync(x => x.RequestId == r.Id);
            if (booked.Amount != amount || booked.State != "Confirmed") throw new RuleViolation("This request was already confirmed and its reservation changed. Review the calendar.");
            return booked;
        }
        if (r.State != "Pending" || r.Revision != revision) throw new RuleViolation("The request changed. Refresh before deciding.");
        if (clock.GetUtcNow() >= r.Expires) throw new RuleViolation("This request expired and cannot be confirmed.");
        var unit = await db.Units.SingleAsync(x => x.Id == r.UnitId && x.PropertyId == p.Id);
        if (!await Available(unit, r.Arrival, r.Departure, r.Guests)) throw new RuleViolation("Inventory changed. These dates cannot be confirmed.");
        var reservation = new Reservation { PropertyId = p.Id, UnitId = unit.Id, RequestId = r.Id, Arrival = r.Arrival, Departure = r.Departure, Guests = r.Guests, Name = r.Name, Phone = r.Phone, Amount = amount, Currency = p.Currency, TermsVersion = r.TermsVersion, Confirmed = clock.GetUtcNow() };
        db.Reservations.Add(reservation); db.Allocations.Add(new InventoryAllocation { PropertyId = p.Id, UnitId = unit.Id, ReservationId = reservation.Id, Start = r.Arrival, End = r.Departure.AddDays(unit.Turnover), Created = clock.GetUtcNow() });
        r.State = "Accepted"; r.Revision++; r.Closed = clock.GetUtcNow();
        Record(p.Id, owner, reservation.Id, "Reservation confirmed", $"{r.Arrival} to {r.Departure}");
        try { await db.SaveChangesAsync(); await tx.CommitAsync(); }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: "23P01" }) { throw new RuleViolation("Inventory changed. These dates cannot be confirmed."); }
        return reservation;
    }
    public async Task<GuestStatus?> Status(string token)
    {
        if (!ValidSecret(token)) return null;
        var hash = Hash(token); var r = await db.Requests.AsNoTracking().SingleOrDefaultAsync(x => x.TokenHash == hash);
        if (r == null) return null;
        var now = clock.GetUtcNow();
        var p = await db.Properties.AsNoTracking().SingleAsync(x => x.Id == r.PropertyId); var u = await db.Units.AsNoTracking().SingleAsync(x => x.Id == r.UnitId);
        var reservation = await db.Reservations.AsNoTracking().SingleOrDefaultAsync(x => x.RequestId == r.Id);
        if (!Matches(hash, r.TokenHash) || !GuestAccess(r, p, reservation)) return null;
        var state = reservation?.State == "Cancelled" ? "Cancelled" : r.State == "Pending" && now >= r.Expires ? "Expired" : r.State;
        return new(state, p.Name, u.Name, reservation?.Arrival ?? r.Arrival, reservation?.Departure ?? r.Departure, reservation?.Guests ?? r.Guests, r.Expires, p.Contact, r.PublicReason, reservation?.Amount, reservation?.Currency,
            state == "Pending", reservation?.State == "Confirmed" && StayRules.Today(now, p.Zone) < reservation.Departure, reservation?.CancellationRequested ?? false);
    }
}
