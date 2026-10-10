using Microsoft.EntityFrameworkCore;
namespace StayWidget.Persistence;

public sealed class StayDb(DbContextOptions<StayDb> options) : DbContext(options)
{
    public DbSet<LocalUser> Users => Set<LocalUser>(); public DbSet<Property> Properties => Set<Property>();
    public DbSet<Unit> Units => Set<Unit>(); public DbSet<ReservationRequest> Requests => Set<ReservationRequest>();
    public DbSet<Reservation> Reservations => Set<Reservation>(); public DbSet<InventoryAllocation> Allocations => Set<InventoryAllocation>();
    public DbSet<ActivityRecord> Activity => Set<ActivityRecord>();
    public DbSet<ProductFeedback> Feedback => Set<ProductFeedback>();
    public DbSet<DailyMetric> Metrics => Set<DailyMetric>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<LocalUser>().HasIndex(x => new { x.Issuer, x.Subject }).IsUnique();
        b.Entity<Property>().HasIndex(x => x.OwnerId).IsUnique();
        b.Entity<Property>().Property(x => x.Revision).IsConcurrencyToken();
        b.Entity<Property>().ToTable(t => t.HasCheckConstraint("PlanValid", "\"Plan\" IN ('Free','Pro')"));
        b.Entity<Property>().HasOne<LocalUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Unit>().HasAlternateKey(x => new { x.PropertyId, x.Id });
        b.Entity<Unit>().HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Unit>().HasIndex(x => new { x.PropertyId, x.Name }).IsUnique();
        b.Entity<Unit>().Property(x => x.Revision).IsConcurrencyToken();
        b.Entity<ReservationRequest>().HasAlternateKey(x => new { x.PropertyId, x.UnitId, x.Id });
        b.Entity<ReservationRequest>().HasOne<Unit>().WithMany().HasForeignKey(x => new { x.PropertyId, x.UnitId }).HasPrincipalKey(x => new { x.PropertyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ReservationRequest>().HasIndex(x => new { x.PropertyId, x.KeyHash }).IsUnique();
        b.Entity<ReservationRequest>().HasIndex(x => x.TokenHash).IsUnique();
        b.Entity<ReservationRequest>().HasIndex(x => x.ActionHash).IsUnique();
        b.Entity<ReservationRequest>().HasIndex(x => new { x.PropertyId, x.Created });
        b.Entity<ReservationRequest>().HasIndex(x => new { x.PropertyId, x.State, x.Created });
        b.Entity<ReservationRequest>().Property(x => x.Revision).IsConcurrencyToken();
        b.Entity<ReservationRequest>().HasIndex(x => new { x.State, x.Expires });
        b.Entity<Reservation>().HasAlternateKey(x => new { x.PropertyId, x.UnitId, x.Id });
        b.Entity<Reservation>().HasOne<ReservationRequest>().WithOne().HasForeignKey<Reservation>(x => new { x.PropertyId, x.UnitId, x.RequestId }).HasPrincipalKey<ReservationRequest>(x => new { x.PropertyId, x.UnitId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Reservation>().HasIndex(x => x.RequestId).IsUnique(); b.Entity<Reservation>().Property(x => x.Amount).HasPrecision(12,2);
        b.Entity<Reservation>().HasOne<Unit>().WithMany().HasForeignKey(x => new { x.PropertyId, x.UnitId }).HasPrincipalKey(x => new { x.PropertyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Reservation>().Property(x => x.Revision).IsConcurrencyToken();
        b.Entity<InventoryAllocation>().HasOne<Reservation>().WithOne().HasForeignKey<InventoryAllocation>(x => new { x.PropertyId, x.UnitId, x.ReservationId }).HasPrincipalKey<Reservation>(x => new { x.PropertyId, x.UnitId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<InventoryAllocation>().HasIndex(x => x.ReservationId).IsUnique();
        b.Entity<InventoryAllocation>().HasOne<Unit>().WithMany().HasForeignKey(x => new { x.PropertyId, x.UnitId }).HasPrincipalKey(x => new { x.PropertyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<InventoryAllocation>().Property(x => x.Revision).IsConcurrencyToken();
        b.Entity<ActivityRecord>().HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ActivityRecord>().HasIndex(x => new { x.PropertyId, x.At });
        b.Entity<ProductFeedback>().HasOne<LocalUser>().WithMany().HasForeignKey(x => x.SubmitterUserId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<ProductFeedback>().HasIndex(x => new { x.SubmitterUserId, x.SubmissionKey }).IsUnique();
        b.Entity<ProductFeedback>().HasIndex(x => new { x.SubmitterUserId, x.Created });
        b.Entity<ProductFeedback>().HasIndex(x => new { x.Status, x.Closed });
        b.Entity<ProductFeedback>().Property(x => x.Revision).IsConcurrencyToken();
        b.Entity<ProductFeedback>().Property(x => x.Body).HasMaxLength(4000);
        b.Entity<ProductFeedback>().Property(x => x.Contact).HasMaxLength(200);
        b.Entity<ProductFeedback>().Property(x => x.Comment).HasMaxLength(4000);
        b.Entity<ProductFeedback>().ToTable(t => t.HasCheckConstraint("FeedbackState", "\"Status\" IN ('New','Reviewed','Planned','InProgress','Closed') AND ((\"Status\"='Closed' AND \"Closed\" IS NOT NULL AND \"CloseReason\" IS NOT NULL AND \"CloseReason\" IN ('Delivered','Duplicate','NotPlanned','CannotReproduce','Spam')) OR (\"Status\"<>'Closed' AND \"Closed\" IS NULL AND \"CloseReason\" IS NULL)) AND ((\"Status\"='New' AND \"Reviewed\" IS NULL) OR (\"Status\"<>'New' AND \"Reviewed\" IS NOT NULL)) AND \"Kind\" IN ('Feedback','Custom')"));
        b.Entity<DailyMetric>().HasKey(x => new { x.Day, x.Event });
        b.Entity<DailyMetric>().Property(x => x.Event).HasMaxLength(40);
    }
}
