using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using StayWidget.Persistence;

namespace StayWidget.Web;

public sealed partial class BookingService
{
    // Durable fair property cursor, transaction-scoped lease, bounded rows. No guest data in logs/audit.
    public async Task<int> Maintain(CancellationToken cancellation = default)
    {
        db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync(cancellation);
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = tx.GetDbTransaction();
        command.CommandText = "SELECT pg_try_advisory_xact_lock(741038221)";
        if (!Equals(await command.ExecuteScalarAsync(cancellation), true)) return 0;
        var now = clock.GetUtcNow();
        var oldEmailIds = await db.ConfirmationEmails.Where(x => x.Created < now.AddDays(-30)).OrderBy(x => x.Created).Take(100).Select(x => x.Id).ToListAsync(cancellation);
        await db.ConfirmationEmails.Where(x => oldEmailIds.Contains(x.Id)).ExecuteDeleteAsync(cancellation);
        var ids = await db.Properties.OrderBy(x => x.MaintenanceAt).ThenBy(x => x.Id).Take(20).Select(x => x.Id).ToListAsync(cancellation);
        int expired = 0;
        foreach (var id in ids.Order())
        {
            var p = await db.Properties.FromSqlInterpolated($"SELECT * FROM \"Properties\" WHERE \"Id\"={id} FOR UPDATE").SingleAsync(cancellation);
            p.MaintenanceAt = now;
            var pending = await db.Requests.Where(x => x.PropertyId == id && x.State == "Pending" && x.Expires <= now)
                .OrderBy(x => x.Expires).ThenBy(x => x.Id).Take(100).ToListAsync(cancellation);
            foreach (var r in pending)
            { r.State = "Expired"; r.Closed = r.Expires; r.Revision++; Record(id, null, r.Id, "Request expired"); expired++; }
            var receiptLimit = now.AddHours(-24);
            foreach (var r in await db.Requests.Where(x => x.PropertyId == id && x.Created <= receiptLimit && x.ProtectedReceipt != "" && !(x.State == "Pending" && x.Email != "")).OrderBy(x => x.Id).Take(100).ToListAsync(cancellation))
                r.ProtectedReceipt = "";
            var requestLimit = now.AddDays(-90);
            foreach (var r in await db.Requests.Where(x => x.PropertyId == id && x.State != "Pending" && x.State != "Accepted" && !x.Redacted &&
                (x.Closed ?? x.Expires) <= requestLimit).OrderBy(x => x.Id).Take(100).ToListAsync(cancellation)) Redact(r);
            var bookingLimit = now.AddMonths(-12);
            var dateLimit = StayWidget.Domain.StayRules.Today(bookingLimit, p.Zone);
            foreach (var block in await db.Allocations.Where(x => x.PropertyId == id && x.ReservationId == null && x.End < dateLimit && x.Reason != "").OrderBy(x => x.Id).Take(100).ToListAsync(cancellation))
                block.Reason = "";
            var oldBookings = await db.Reservations.Where(x => x.PropertyId == id && !x.Redacted && x.Departure < dateLimit &&
                (x.Cancelled == null || x.Cancelled < bookingLimit)).OrderBy(x => x.Id).Take(100).ToListAsync(cancellation);
            foreach (var r in oldBookings)
            {
                r.Name = ""; r.Phone = ""; r.Redacted = true; r.Revision++;
                if (r.RequestId.HasValue)
                { var request = await db.Requests.SingleAsync(x => x.Id == r.RequestId && x.PropertyId == id, cancellation); Redact(request); }
            }
            // Never remove an upcoming booking, even if cancelled long ago. Keep nonpersonal history for 24 months.
            var historyDate = StayWidget.Domain.StayRules.Today(now.AddMonths(-24), p.Zone);
            var historyTime = now.AddMonths(-24);
            foreach (var r in await db.Reservations.Where(x => x.PropertyId == id && x.Redacted && x.Departure < historyDate &&
                (x.Cancelled == null || x.Cancelled < historyTime)).OrderBy(x => x.Id).Take(100).ToListAsync(cancellation))
            {
                db.Allocations.RemoveRange(await db.Allocations.Where(x => x.PropertyId == id && x.ReservationId == r.Id).ToListAsync(cancellation));
                db.Reservations.Remove(r);
            }
            db.Allocations.RemoveRange(await db.Allocations.Where(x => x.PropertyId == id && x.ReservationId == null && x.End < historyDate).OrderBy(x => x.Id).Take(100).ToListAsync(cancellation));
            var oldRequests = await db.Requests.Where(x => x.PropertyId == id && x.Redacted && x.Departure < historyDate &&
                !db.Reservations.Any(r => r.RequestId == x.Id)).OrderBy(x => x.Id).Take(100).ToListAsync(cancellation);
            db.Requests.RemoveRange(oldRequests);
            db.Activity.RemoveRange(await db.Activity.Where(x => x.PropertyId == id && x.At < bookingLimit).OrderBy(x => x.Id).Take(100).ToListAsync(cancellation));
        }
        await db.SaveChangesAsync(cancellation); await tx.CommitAsync(cancellation); return expired;
    }

    private static void Redact(ReservationRequest r)
    {
        r.Name = ""; r.Phone = ""; r.Email = ""; r.PublicReason = ""; r.Policies = ""; r.ProtectedReceipt = "";
        r.Redacted = true; r.Revision++;
        // Hashes remain as non-contact retry tombstones until the 24-month history sweep.
    }
}

public sealed class MaintenanceWorker(IServiceScopeFactory scopes, ILogger<MaintenanceWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    var count = await scope.ServiceProvider.GetRequiredService<BookingService>().Maintain(stoppingToken);
                    if (count > 0) log.LogInformation("Maintenance expired {Count} requests.", count);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception e) { log.LogWarning("Maintenance failed ({FailureType}); retry on next bounded tick.", e.GetType().Name); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
