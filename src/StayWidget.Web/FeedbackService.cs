using Microsoft.EntityFrameworkCore;
using StayWidget.Domain;
using StayWidget.Persistence;
namespace StayWidget.Web;

public sealed class FeedbackService(StayDb db, TimeProvider clock)
{
    public async Task Submit(Guid user, Guid key, string body, string kind, string contact, bool acknowledged)
    {
        body = body.Trim(); contact = contact.Trim();
        if (key == Guid.Empty || body.Length is < 10 or > 4000 || contact.Length > 200 || kind is not ("Feedback" or "Custom") || !acknowledged)
            throw new RuleViolation("Describe your task or requirements in 10–4,000 characters and acknowledge the feedback notice. Contact is optional (200 characters maximum).");
        await using var tx = await db.Database.BeginTransactionAsync(); db.ChangeTracker.Clear();
        _ = await db.Users.FromSqlInterpolated($"SELECT * FROM \"Users\" WHERE \"Id\"={user} FOR UPDATE").SingleOrDefaultAsync() ?? throw new RuleViolation("Sign in again before submitting.");
        var old = await db.Feedback.SingleOrDefaultAsync(x => x.SubmitterUserId == user && x.SubmissionKey == key);
        if (old != null)
        {
            if (old.Body != body || old.Kind != kind || old.Contact != contact) throw new RuleViolation("This submission was already saved with different content. Open a new form to submit something else.");
            await tx.CommitAsync(); return;
        }
        var now = clock.GetUtcNow(); var day = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        if (await db.Feedback.CountAsync(x => x.SubmitterUserId == user && x.Created >= day) >= 5) throw new RuleViolation("Five submissions per account per UTC day are permitted. Your previous reports are saved; retry tomorrow.");
        db.Feedback.Add(new ProductFeedback { SubmitterUserId = user, SubmissionKey = key, Body = body, Kind = kind, Contact = contact, Created = now, Updated = now });
        await db.SaveChangesAsync(); await tx.CommitAsync();
    }
    public async Task Cleanup(CancellationToken token)
    {
        // Same user-row lock as submission/direct review. Bounded, recheck after acquiring it.
        var now = clock.GetUtcNow(); var cutoff = now.AddMonths(-12);
        var users = await db.Feedback.Where(x => x.Closed < cutoff || x.Status == "New" && x.Created < cutoff)
            .OrderBy(x => x.Created).Select(x => x.SubmitterUserId).Distinct().Take(20).ToListAsync(token);
        foreach (var user in users)
        {
            db.ChangeTracker.Clear(); await using var tx = await db.Database.BeginTransactionAsync(token);
            _ = await db.Users.FromSqlInterpolated($"SELECT * FROM \"Users\" WHERE \"Id\"={user} FOR UPDATE").SingleOrDefaultAsync(token);
            var rows = await db.Feedback.Where(x => x.SubmitterUserId == user && (x.Closed < cutoff || x.Status == "New" && x.Created < cutoff)).OrderBy(x => x.Created).Take(100).ToListAsync(token);
            db.Feedback.RemoveRange(rows); await db.SaveChangesAsync(token); await tx.CommitAsync(token);
        }
    }
}
