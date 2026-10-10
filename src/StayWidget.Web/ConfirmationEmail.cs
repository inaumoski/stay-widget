using System.Net;
using System.Net.Mail;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using StayWidget.Domain;
using StayWidget.Persistence;

namespace StayWidget.Web;

public sealed class EmailSettings
{
    public string Mode { get; set; } = "Disabled";
    public string Sender { get; set; } = "noreply@funtoolie.com";
    public string ApiKey { get; set; } = "";
    public string SinkDirectory { get; set; } = "";
    public int DailyAttemptLimit { get; set; } = 200;
    public string Notice => Mode switch
    {
        "Brevo" => "Confirmation email is queued after owner approval. Save your private status link in case delivery fails. No email is sent while your request is pending.",
        "Sink" => "Local preview: confirmation email is saved privately for testing and is not sent to an inbox. Save your private status link.",
        _ => "Email delivery is disabled. Save your private status link and contact the property for its decision."
    };
    public static string Address(string value)
    {
        value = value.Trim();
        if (value.Length is < 3 or > 254 || value.Any(char.IsControl) || !MailAddress.TryCreate(value, out var address) || address.Address != value || !address.Host.Contains('.'))
            throw new RuleViolation("Enter one valid email address without a display name.");
        return value;
    }
    public void Validate(bool development)
    {
        if (Mode is not ("Disabled" or "Sink" or "Brevo") || DailyAttemptLimit is < 1 or > 200) throw new InvalidOperationException("Invalid Email mode or daily attempt limit (1–200).");
        if (Mode == "Sink" && (!development || !Path.IsPathFullyQualified(SinkDirectory))) throw new InvalidOperationException("Email sink requires Development and an absolute private directory outside webroot.");
        if (Mode == "Brevo")
        {
            _ = Address(Sender);
            if (development || string.IsNullOrWhiteSpace(ApiKey)) throw new InvalidOperationException("Brevo requires non-Development configuration and a private API key; development uses the sink.");
        }
    }
}

public sealed record ConfirmationMessage(string To, string Text, string Link);
public sealed record EmailResult(string State, string Failure = "");
public interface IConfirmationTransport { Task<EmailResult> Send(Guid id, ConfirmationMessage message, CancellationToken cancellation); }

public sealed class ConfirmationTransport(EmailSettings settings, IHttpClientFactory clients) : IConfirmationTransport
{
    public async Task<EmailResult> Send(Guid id, ConfirmationMessage message, CancellationToken cancellation)
    {
        if (settings.Mode == "Sink")
        {
            Directory.CreateDirectory(settings.SinkDirectory);
            // Stable filename makes a repeated local attempt idempotent. Private Development data only.
            await File.WriteAllTextAsync(Path.Combine(settings.SinkDirectory, id.ToString("N") + ".json"), JsonSerializer.Serialize(new { Sender = settings.Sender, Subject = "Reservation confirmed", message.To, message.Text }), cancellation);
            return new("LocalPreview");
        }
        if (settings.Mode != "Brevo") return new("Queued", "SendingDisabled");
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
        request.Headers.Add("api-key", settings.ApiKey);
        request.Content = JsonContent.Create(new
        {
            sender = new { email = settings.Sender, name = "StayWidget" },
            to = new[] { new { email = message.To } }, subject = "Reservation confirmed",
            textContent = message.Text,
            htmlContent = "<html><body><pre>" + WebUtility.HtmlEncode(message.Text) + "</pre><p><a href=\"" + WebUtility.HtmlEncode(message.Link) + "\">View reservation or request cancellation</a></p></body></html>"
        });
        try
        {
            using var response = await clients.CreateClient("Brevo").SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation);
            // Acceptance is not inbox delivery. Do not log provider bodies or private URLs.
            if (response.StatusCode == HttpStatusCode.Created) return new("Accepted");
            if ((int)response.StatusCode == 429) return new("Queued", "ProviderRateLimited");
            if ((int)response.StatusCode >= 500) return new("Unknown", "ProviderOutcomeUnknown");
            return new("Failed", "ProviderRejected");
        }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException)
        {
            // The provider may have accepted before the connection failed. Never blindly resend.
            return new("Unknown", "ProviderOutcomeUnknown");
        }
    }
}

public sealed partial class BookingService
{
    private void QueueConfirmation(Property p, Unit unit, ReservationRequest r, Reservation reservation)
    {
        if (r.Email.Length == 0) return; // Existing/manual bookings have no guest email; never invent one.
        var now = clock.GetUtcNow();
        var email = new ConfirmationEmail { PropertyId = p.Id, RequestId = r.Id, Created = now, NextAttempt = now };
        if (r.CapabilitiesRevoked || r.ProtectedReceipt.Length == 0)
        { email.State = "Cancelled"; email.Failure = "PrivateLinkUnavailable"; email.Finished = now; }
        else
        {
            try
            {
            var tokens = JsonSerializer.Deserialize<Tokens>(protector.Unprotect(r.ProtectedReceipt))!;
            var link = (site?.Value ?? "https://localhost:7259") + "/GuestStatus#" + tokens.View + "." + tokens.Action;
            var text = $"Reservation confirmed\n\nProperty: {p.Name}\nAccommodation: {unit.Name}\nCheck-in: {reservation.Arrival:yyyy-MM-dd}\nCheck-out: {reservation.Departure:yyyy-MM-dd}\nGuests: {reservation.Guests}\nAgreed total: {reservation.Amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)} {reservation.Currency}\n\nOwner contact: {p.Contact}\n\nView your reservation or request cancellation:\n{link}\n\nThis private link grants access to your reservation. Do not share it. Opening it does not cancel anything. Review and explicitly request cancellation on the page; the owner must accept under the property's policies before booked dates are released. Contact the owner for policy, date or payment questions. StayWidget has not collected payment.\n\nThe private status page is the source of truth for later changes. This mailbox does not accept replies.";
            email.ProtectedMessage = protection.CreateProtector("StayWidget.ConfirmationEmail.v1").Protect(JsonSerializer.Serialize(new ConfirmationMessage(r.Email, text, link)));
            }
            catch (Exception e) when (e is System.Security.Cryptography.CryptographicException or JsonException)
            { email.State = "Failed"; email.Failure = "PrivateLinkUnrecoverable"; email.Finished = now; }
        }
        db.ConfirmationEmails.Add(email);
    }
}

public sealed class ConfirmationDispatcher(StayDb db, TimeProvider clock, IDataProtectionProvider protection, IConfirmationTransport transport, EmailSettings settings)
{
    public async Task<bool> DispatchOne(CancellationToken cancellation = default)
    {
        if (settings.Mode == "Disabled") return false;
        db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync(cancellation);
        await using var lease = db.Database.GetDbConnection().CreateCommand();
        lease.Transaction = tx.GetDbTransaction(); lease.CommandText = "SELECT pg_try_advisory_xact_lock(741038223)";
        if (!Equals(await lease.ExecuteScalarAsync(cancellation), true)) return false;
        var now = clock.GetUtcNow();
        var cutoff = now.AddDays(-30);
        var claimed = false;
        var oldIds = await db.ConfirmationEmails.Where(x => x.Created < cutoff).OrderBy(x => x.Created).Take(100).Select(x => x.Id).ToListAsync(cancellation);
        await db.ConfirmationEmails.Where(x => oldIds.Contains(x.Id)).ExecuteDeleteAsync(cancellation);
        var email = await db.ConfirmationEmails.Where(x => x.State == "Queued" && x.NextAttempt <= now).OrderBy(x => x.Created).FirstOrDefaultAsync(cancellation);
        if (email == null) { await tx.CommitAsync(cancellation); return false; }
        var r = await db.Requests.AsNoTracking().SingleAsync(x => x.Id == email.RequestId && x.PropertyId == email.PropertyId, cancellation);
        var p = await db.Properties.AsNoTracking().SingleAsync(x => x.Id == email.PropertyId, cancellation);
        var reservation = await db.Reservations.AsNoTracking().SingleOrDefaultAsync(x => x.RequestId == r.Id && x.PropertyId == p.Id, cancellation);
        if (r.CapabilitiesRevoked || r.Redacted || p.Suspended || reservation?.State != "Confirmed" || reservation.Revision != 0 || now >= email.Created.AddDays(7) || StayRules.Today(now, p.Zone) >= reservation.Departure)
        { email.State = "Cancelled"; email.Failure = "ReservationOrLinkChanged"; }
        else
        {
            var day = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
            // Conservative shared-product budget: counts attempts on recently scheduled rows, not claimed inbox delivery.
            var used = await db.ConfirmationEmails.Where(x => x.LastAttempt >= day && x.LastAttempt < day.AddDays(1)).SumAsync(x => x.Attempts, cancellation);
            if (used >= settings.DailyAttemptLimit) { email.NextAttempt = day.AddDays(1); await db.SaveChangesAsync(cancellation); await tx.CommitAsync(cancellation); return true; }
            email.Attempts++; email.LastAttempt = now;
            // Commit an indeterminate attempt BEFORE external I/O. A crash cannot silently replay a send.
            email.State = "Unknown"; email.Failure = "ProviderOutcomeUnknown";
            await db.SaveChangesAsync(cancellation); await tx.CommitAsync(cancellation);
            claimed = true;
            try
            {
                var message = JsonSerializer.Deserialize<ConfirmationMessage>(protection.CreateProtector("StayWidget.ConfirmationEmail.v1").Unprotect(email.ProtectedMessage))!;
                var result = await transport.Send(email.Id, message, cancellation);
                email.State = result.State; email.Failure = result.Failure;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            { email.State = "Failed"; email.Failure = "LocalDeliveryFailure"; }
            if (email.State == "Queued")
            {
                if (email.Attempts >= 5) { email.State = "Failed"; email.Failure = "RetryLimitReached"; }
                else email.NextAttempt = now.AddMinutes(Math.Pow(2, email.Attempts) * 5);
            }
        }
        if (email.State != "Queued") { email.Finished = now; email.ProtectedMessage = ""; }
        await db.SaveChangesAsync(cancellation);
        if (!claimed) await tx.CommitAsync(cancellation);
        return true;
    }
}

public sealed class ConfirmationEmailWorker(IServiceScopeFactory scopes, ILogger<ConfirmationEmailWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<ConfirmationDispatcher>().DispatchOne(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception e) { log.LogWarning("Confirmation email worker failed ({FailureType}); inspect private queue status.", e.GetType().Name); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
