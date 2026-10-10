using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using StayWidget.Domain;
using StayWidget.Persistence;
namespace StayWidget.Web;

public sealed class GuestLinkUnavailable : Exception;
public sealed partial class BookingService
{
    private static bool ValidSecret(string token) => token.Length == 64 && token.All(Uri.IsHexDigit);
    private static bool Matches(string hash, string? expected) => expected != null &&
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(hash), Encoding.UTF8.GetBytes(expected));
    private DateTimeOffset CapabilityEnd(ReservationRequest r, Reservation? reservation)
    {
        var end = reservation != null ? new DateTimeOffset(reservation.Departure.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)).AddDays(30) : (r.Closed ?? r.Expires).AddDays(30);
        if (reservation?.Cancelled is { } cancelled && cancelled.AddDays(30) < end) end = cancelled.AddDays(30);
        var ceiling = r.Created.AddDays(424); return end < ceiling ? end : ceiling;
    }
    private bool GuestAccess(ReservationRequest r, Property p, Reservation? reservation) =>
        !r.CapabilitiesRevoked && !r.Redacted && !p.Suspended && clock.GetUtcNow() < CapabilityEnd(r, reservation);

    public async Task GuestAction(string token, string action, bool confirmed)
    {
        if (!ValidSecret(token)) throw new GuestLinkUnavailable();
        var hash = Hash(token); db.ChangeTracker.Clear();
        var propertyId = await db.Requests.AsNoTracking().Where(x => x.ActionHash == hash).Select(x => (Guid?)x.PropertyId).SingleOrDefaultAsync();
        if (propertyId == null) throw new GuestLinkUnavailable();
        await using var tx = await db.Database.BeginTransactionAsync();
        var p = await db.Properties.FromSqlInterpolated($"SELECT * FROM \"Properties\" WHERE \"Id\"={propertyId.Value} FOR UPDATE").SingleAsync();
        var r = await db.Requests.SingleOrDefaultAsync(x => x.PropertyId == p.Id && x.ActionHash == hash) ?? throw new GuestLinkUnavailable();
        var reservation = await db.Reservations.SingleOrDefaultAsync(x => x.PropertyId == p.Id && x.RequestId == r.Id);
        if (!Matches(hash, r.ActionHash) || !GuestAccess(r, p, reservation)) throw new GuestLinkUnavailable();
        if (!confirmed) throw new RuleViolation("Review and explicitly confirm this action.");
        if (action == "withdraw")
        {
            if (r.State == "Withdrawn") return;
            if (r.State != "Pending" || clock.GetUtcNow() >= r.Expires) throw new RuleViolation("This request changed or expired. Refresh its status before deciding.");
            r.State = "Withdrawn"; r.Closed = clock.GetUtcNow(); r.Revision++;
            Record(p.Id, null, r.Id, "Guest withdrew pending request");
        }
        else if (action == "request-cancellation")
        {
            if (reservation?.State != "Confirmed" || StayRules.Today(clock.GetUtcNow(), p.Zone) >= reservation.Departure)
                throw new RuleViolation("This reservation is no longer eligible for a cancellation request. Contact the property.");
            if (reservation.CancellationRequested) return;
            reservation.CancellationRequested = true; reservation.Revision++;
            Record(p.Id, null, reservation.Id, "Guest requested cancellation", "Inventory remains occupied until owner cancellation.");
        }
        else throw new RuleViolation("Unknown guest action.");
        await Save(); await tx.CommitAsync();
    }

    public async Task RevokeGuestLinks(Guid owner, Guid id, int revision, bool reviewed)
    {
        if (!reviewed) throw new RuleViolation("Acknowledge that the guest's saved links will stop working. Contact the guest directly.");
        await using var tx = await db.Database.BeginTransactionAsync(); var p = await Lock(owner);
        var r = await db.Requests.SingleOrDefaultAsync(x => x.PropertyId == p.Id && x.Id == id) ?? throw new RuleViolation("Request not found.");
        if (r.CapabilitiesRevoked) return;
        if (r.Revision != revision) throw new RuleViolation("Request changed. Refresh before revoking its links.");
        r.CapabilitiesRevoked = true; r.ProtectedReceipt = ""; r.Revision++;
        Record(p.Id, owner, id, "Private guest links revoked"); await Save(); await tx.CommitAsync();
    }

    public async Task KeepReservation(Guid owner, Guid id, int revision)
    {
        await using var tx = await db.Database.BeginTransactionAsync(); var p = await Lock(owner);
        var r = await db.Reservations.SingleOrDefaultAsync(x => x.PropertyId == p.Id && x.Id == id) ?? throw new RuleViolation("Reservation not found.");
        if (!r.CancellationRequested) return;
        if (r.Revision != revision || r.State != "Confirmed") throw new RuleViolation("Reservation changed. Refresh before resolving cancellation.");
        r.CancellationRequested = false; r.Revision++;
        Record(p.Id, owner, id, "Cancellation request reviewed; reservation kept"); await Save(); await tx.CommitAsync();
    }

    public async Task SaveEmbedSettings(Guid owner, int revision, string origins, string accent, bool attested, bool development)
    {
        var list = origins.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(x => EmbedPolicy.Origin(x, development)).Distinct().ToArray();
        if (list.Length > 5 || list.Length > 0 && !attested) throw new RuleViolation("Approve at most five exact origins and attest that you are authorized to install on them.");
        accent = EmbedPolicy.Accent(accent);
        await using var tx = await db.Database.BeginTransactionAsync(); var p = await Lock(owner);
        if (p.Revision != revision) throw new RuleViolation("Property settings changed. Refresh before saving installation settings.");
        p.EmbedOrigins = list; p.AccentColor = accent; p.EmbedAttestedAt = list.Length > 0 ? clock.GetUtcNow() : null; p.Revision++;
        Record(p.Id, owner, p.Id, "Embed origins/theme saved", "Owner-attested origins; no domain-verification fetch performed.");
        await Save(); await tx.CommitAsync();
    }
}
