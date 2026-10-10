using Microsoft.EntityFrameworkCore;
using StayWidget.Domain;
using StayWidget.Persistence;
namespace StayWidget.Web;

public static class ProductPlans
{
    public const int StoredUnitLimit = 5;
    public static readonly DateTimeOffset PromotionEnd = new(2027, 2, 1, 0, 0, 0, TimeSpan.Zero);
    public static int ActiveLimit(Property p) => p.Plan == "Free" ? 1 : 5;
    public static bool ShowBranding(Property p) => p.Plan == "Free" || !p.HideBranding;
    public static string Offer(DateTimeOffset now) => now < PromotionEnd
        ? "Pro: 100% discount until 1 February 2027, 00:00 UTC. No card or automatic charge."
        : "Pro: continued free early access pending an approved billing rollout. No automatic charge or downgrade.";
}
public sealed partial class BookingService
{
    public async Task SetPlan(Guid owner, int revision, string plan, bool hideBranding, bool acknowledged)
    {
        if (plan is not ("Free" or "Pro") || !acknowledged) throw new RuleViolation("Select Free or free early-access Pro and acknowledge that existing work is preserved.");
        await using var tx = await db.Database.BeginTransactionAsync(); var p = await Lock(owner);
        if (p.Revision != revision) throw new RuleViolation("Plan settings changed. Refresh before saving.");
        p.Plan = plan; p.HideBranding = plan == "Pro" && hideBranding; p.Revision++;
        Record(p.Id, owner, p.Id, "Plan preference updated", "No charge, downgrade or inventory changes.");
        await Save(); await tx.CommitAsync();
    }
    public async Task AcknowledgeInstallation(Guid owner, int revision, bool acknowledged)
    {
        if (!acknowledged) throw new RuleViolation("Acknowledge the installation instructions before recording activation.");
        await using var tx = await db.Database.BeginTransactionAsync(); var p = await Lock(owner);
        if (p.InstallationAcknowledgedAt != null) return;
        if (p.Revision != revision) throw new RuleViolation("Settings changed. Refresh before recording activation.");
        p.InstallationAcknowledgedAt = clock.GetUtcNow(); p.Revision++;
        Record(p.Id, owner, p.Id, "Installation instructions acknowledged");
        await Save(); await tx.CommitAsync(); metrics?.Record("installation");
    }
}
