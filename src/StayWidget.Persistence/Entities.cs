namespace StayWidget.Persistence;

public sealed class LocalUser { public Guid Id { get; set; } = Guid.NewGuid(); public string Issuer { get; set; } = ""; public string Subject { get; set; } = ""; public string Name { get; set; } = "Owner"; }
public sealed class Property
{
    public Guid Id { get; set; } = Guid.NewGuid(); public Guid OwnerId { get; set; }
    public string Name { get; set; } = ""; public string Contact { get; set; } = "";
    public string Zone { get; set; } = "Europe/Skopje"; public string Currency { get; set; } = "EUR";
    public string Policies { get; set; } = ""; public string TermsVersion { get; set; } = "1";
    public bool Published { get; set; } = true;
    public int Revision { get; set; }
    public DateTimeOffset MaintenanceAt { get; set; } = DateTimeOffset.UnixEpoch;
    public string[] EmbedOrigins { get; set; } = [];
    public DateTimeOffset? EmbedAttestedAt { get; set; }
    public string AccentColor { get; set; } = "#12616A";
    public bool Suspended { get; set; }
    public string Plan { get; set; } = "Pro";
    public bool HideBranding { get; set; }
    public DateTimeOffset? InstallationAcknowledgedAt { get; set; }
}
public sealed class Unit
{
    public Guid Id { get; set; } = Guid.NewGuid(); public Guid PropertyId { get; set; }
    public string Name { get; set; } = ""; public int Capacity { get; set; } = 2;
    public DateOnly OpenFrom { get; set; } public DateOnly OpenTo { get; set; }
    public int Turnover { get; set; } public bool Enabled { get; set; } = true;
    public int MinNights { get; set; } = 1; public int MaxNights { get; set; } = 28;
    public int Revision { get; set; }
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
    public DateTimeOffset? Closed { get; set; }
    public bool Redacted { get; set; }
    public string? ActionHash { get; set; }
    public bool CapabilitiesRevoked { get; set; }
}
public sealed class Reservation
{
    public Guid Id { get; set; } = Guid.NewGuid(); public Guid PropertyId { get; set; } public Guid UnitId { get; set; }
    public Guid? RequestId { get; set; } public DateOnly Arrival { get; set; } public DateOnly Departure { get; set; }
    public decimal Amount { get; set; } public string Currency { get; set; } = "";
    public string TermsVersion { get; set; } = ""; public DateTimeOffset Confirmed { get; set; }
    public string State { get; set; } = "Confirmed"; public int Guests { get; set; } = 1;
    public string Name { get; set; } = ""; public string Phone { get; set; } = "";
    public int Revision { get; set; } public DateTimeOffset? Cancelled { get; set; }
    public bool CancellationRequested { get; set; }
    public bool Redacted { get; set; } public string CommandHash { get; set; } = "";
}
public sealed class InventoryAllocation
{
    public Guid Id { get; set; } = Guid.NewGuid(); public Guid PropertyId { get; set; } public Guid UnitId { get; set; }
    public Guid? ReservationId { get; set; } public DateOnly Start { get; set; } public DateOnly End { get; set; }
    public bool Active { get; set; } = true; public string Reason { get; set; } = "";
    public int Revision { get; set; } public DateTimeOffset Created { get; set; }
    public string CommandHash { get; set; } = "";
}

public sealed class ActivityRecord
{
    public Guid Id { get; set; } = Guid.NewGuid(); public Guid PropertyId { get; set; }
    public Guid? ActorId { get; set; } public Guid EntityId { get; set; }
    public string Action { get; set; } = ""; public string Details { get; set; } = "";
    public DateTimeOffset At { get; set; }
}

public sealed class ProductFeedback
{
    public Guid Id { get; set; } = Guid.NewGuid(); public Guid SubmitterUserId { get; set; }
    public Guid SubmissionKey { get; set; } public string Body { get; set; } = "";
    public string NoticeVersion { get; set; } = "preview-2026-10-10";
    public string Kind { get; set; } = "Feedback"; public string Contact { get; set; } = "";
    public string Comment { get; set; } = ""; public string? Response { get; set; }
    public string Status { get; set; } = "New"; public string? CloseReason { get; set; }
    public DateTimeOffset Created { get; set; } public DateTimeOffset Updated { get; set; }
    public DateTimeOffset? Reviewed { get; set; } public DateTimeOffset? Closed { get; set; }
    public int Revision { get; set; }
}
public sealed class DailyMetric
{
    public DateOnly Day { get; set; } public string Event { get; set; } = ""; public long Count { get; set; }
}
