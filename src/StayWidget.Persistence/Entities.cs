namespace StayWidget.Persistence;

public sealed class LocalUser { public Guid Id { get; set; } = Guid.NewGuid(); public string Issuer { get; set; } = ""; public string Subject { get; set; } = ""; public string Name { get; set; } = "Owner"; }
public sealed class Property
{
    public Guid Id { get; set; } = Guid.NewGuid(); public Guid OwnerId { get; set; }
    public string Name { get; set; } = ""; public string Contact { get; set; } = "";
    public string Zone { get; set; } = "Europe/Skopje"; public string Currency { get; set; } = "EUR";
    public string Policies { get; set; } = ""; public string TermsVersion { get; set; } = "1";
}
public sealed class Unit
{
    public Guid Id { get; set; } = Guid.NewGuid(); public Guid PropertyId { get; set; }
    public string Name { get; set; } = ""; public int Capacity { get; set; } = 2;
    public DateOnly OpenFrom { get; set; } public DateOnly OpenTo { get; set; }
    public int Turnover { get; set; } public bool Enabled { get; set; } = true;
}
public sealed class ReservationRequest
{
    public Guid Id { get; set; } = Guid.NewGuid(); public Guid PropertyId { get; set; } public Guid UnitId { get; set; }
    public DateOnly Arrival { get; set; } public DateOnly Departure { get; set; } public int Guests { get; set; }
    public string Name { get; set; } = ""; public string Phone { get; set; } = "";
    public string State { get; set; } = "Pending"; public string PublicReason { get; set; } = "";
    public string Policies { get; set; } = ""; public string TermsVersion { get; set; } = "";
    public DateTimeOffset Created { get; set; } public DateTimeOffset Expires { get; set; }
    public string KeyHash { get; set; } = ""; public string PayloadHash { get; set; } = "";
    public string TokenHash { get; set; } = ""; public string ProtectedReceipt { get; set; } = "";
    public int Revision { get; set; }
}
public sealed class Reservation
{
    public Guid Id { get; set; } = Guid.NewGuid(); public Guid PropertyId { get; set; } public Guid UnitId { get; set; }
    public Guid RequestId { get; set; } public DateOnly Arrival { get; set; } public DateOnly Departure { get; set; }
    public decimal Amount { get; set; } public string Currency { get; set; } = "";
    public string TermsVersion { get; set; } = ""; public DateTimeOffset Confirmed { get; set; }
}
public sealed class InventoryAllocation
{
    public Guid Id { get; set; } = Guid.NewGuid(); public Guid PropertyId { get; set; } public Guid UnitId { get; set; }
    public Guid ReservationId { get; set; } public DateOnly Start { get; set; } public DateOnly End { get; set; }
}
