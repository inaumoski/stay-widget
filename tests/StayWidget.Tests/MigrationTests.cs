using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StayWidget.Persistence;
using Xunit;
namespace StayWidget.Tests;

public sealed class MigrationTests
{
    [Fact] public async Task M1UpgradePreservesLiveInventoryAndCopiesGuestSnapshots()
    {
        var fixture = new TestDatabase(); await fixture.InitializeAsync();
        try
        {
            var owner=Guid.NewGuid();var property=Guid.NewGuid();var unit=Guid.NewGuid();var request=Guid.NewGuid();var reservation=Guid.NewGuid();var allocation=Guid.NewGuid();
            var arrival=DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10);var departure=arrival.AddDays(2);var now=DateTimeOffset.UtcNow;
            await using(var db=fixture.Db())
            {
                await db.GetService<IMigrator>().MigrateAsync("20261010133904_InitialInventory");
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"Users\" (\"Id\",\"Issuer\",\"Subject\",\"Name\") VALUES ({owner},{"urn:upgrade"},{"owner"},{"Owner"})");
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"Properties\" (\"Id\",\"OwnerId\",\"Name\",\"Contact\",\"Zone\",\"Currency\",\"Policies\",\"TermsVersion\") VALUES ({property},{owner},{"Upgrade Cottage"},{"Phone host"},{"UTC"},{"EUR"},{"Original property terms"},{"1"})");
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"Units\" (\"Id\",\"PropertyId\",\"Name\",\"Capacity\",\"OpenFrom\",\"OpenTo\",\"Turnover\",\"Enabled\") VALUES ({unit},{property},{"Room"},{2},{arrival},{departure.AddDays(365)},{0},{true})");
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"Requests\" (\"Id\",\"PropertyId\",\"UnitId\",\"Arrival\",\"Departure\",\"Guests\",\"Name\",\"Phone\",\"State\",\"PublicReason\",\"Policies\",\"TermsVersion\",\"Created\",\"Expires\",\"KeyHash\",\"PayloadHash\",\"TokenHash\",\"ProtectedReceipt\",\"Revision\") VALUES ({request},{property},{unit},{arrival},{departure},{2},{"Original guest"},{"+389 70 000 001"},{"Accepted"},{""},{"Original property terms"},{"1"},{now},{now.AddHours(48)},{"key"},{"payload"},{"token"},{""},{1})");
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"Reservations\" (\"Id\",\"PropertyId\",\"UnitId\",\"RequestId\",\"Arrival\",\"Departure\",\"Amount\",\"Currency\",\"TermsVersion\",\"Confirmed\") VALUES ({reservation},{property},{unit},{request},{arrival},{departure},{100m},{"EUR"},{"1"},{now})");
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"Allocations\" (\"Id\",\"PropertyId\",\"UnitId\",\"ReservationId\",\"Start\",\"End\") VALUES ({allocation},{property},{unit},{reservation},{arrival},{departure})");
                await db.Database.MigrateAsync();
            }
            await using var check=fixture.Db();
            Assert.True((await check.Properties.SingleAsync()).Published);Assert.True((await check.Allocations.SingleAsync()).Active);
            var u=await check.Units.SingleAsync();Assert.Equal(1,u.MinNights);Assert.Equal(28,u.MaxNights);
            var r=await check.Reservations.SingleAsync();Assert.Equal("Confirmed",r.State);Assert.Equal(2,r.Guests);Assert.Equal("Original guest",r.Name);Assert.Equal("+389 70 000 001",r.Phone);
            Assert.False(await fixture.Service(check).Available(u,arrival,departure,2));
        }
        finally { await fixture.DisposeAsync(); }
    }
}
