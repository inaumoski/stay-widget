using Microsoft.EntityFrameworkCore;
namespace StayWidget.Persistence;

public sealed class StayDb(DbContextOptions<StayDb> options) : DbContext(options)
{
    public DbSet<LocalUser> Users => Set<LocalUser>(); public DbSet<Property> Properties => Set<Property>();
    public DbSet<Unit> Units => Set<Unit>(); public DbSet<ReservationRequest> Requests => Set<ReservationRequest>();
    public DbSet<Reservation> Reservations => Set<Reservation>(); public DbSet<InventoryAllocation> Allocations => Set<InventoryAllocation>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<LocalUser>().HasIndex(x => new { x.Issuer, x.Subject }).IsUnique();
        b.Entity<Property>().HasIndex(x => x.OwnerId).IsUnique();
        b.Entity<Property>().HasOne<LocalUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Unit>().HasAlternateKey(x => new { x.PropertyId, x.Id });
        b.Entity<Unit>().HasOne<Property>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Unit>().HasIndex(x => new { x.PropertyId, x.Name }).IsUnique();
        b.Entity<ReservationRequest>().HasAlternateKey(x => new { x.PropertyId, x.UnitId, x.Id });
        b.Entity<ReservationRequest>().HasOne<Unit>().WithMany().HasForeignKey(x => new { x.PropertyId, x.UnitId }).HasPrincipalKey(x => new { x.PropertyId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ReservationRequest>().HasIndex(x => new { x.PropertyId, x.KeyHash }).IsUnique();
        b.Entity<ReservationRequest>().HasIndex(x => x.TokenHash).IsUnique();
        b.Entity<ReservationRequest>().HasIndex(x => new { x.PropertyId, x.State, x.Created });
        b.Entity<ReservationRequest>().Property(x => x.Revision).IsConcurrencyToken();
        b.Entity<Reservation>().HasAlternateKey(x => new { x.PropertyId, x.UnitId, x.Id });
        b.Entity<Reservation>().HasOne<ReservationRequest>().WithOne().HasForeignKey<Reservation>(x => new { x.PropertyId, x.UnitId, x.RequestId }).HasPrincipalKey<ReservationRequest>(x => new { x.PropertyId, x.UnitId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Reservation>().HasIndex(x => x.RequestId).IsUnique(); b.Entity<Reservation>().Property(x => x.Amount).HasPrecision(12,2);
        b.Entity<InventoryAllocation>().HasOne<Reservation>().WithOne().HasForeignKey<InventoryAllocation>(x => new { x.PropertyId, x.UnitId, x.ReservationId }).HasPrincipalKey<Reservation>(x => new { x.PropertyId, x.UnitId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<InventoryAllocation>().HasIndex(x => x.ReservationId).IsUnique();
    }
}
