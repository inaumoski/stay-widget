using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using StayWidget.Domain;
using StayWidget.Persistence;
namespace StayWidget.Web.Pages.Owner;

public sealed class ReservationModel(StayDb db, BookingService service, TimeProvider clock) : PageModel
{
    public List<Unit> Units { get; private set; } = [];
    public string State { get; private set; } = "New";
    public bool CancellationRequested { get; private set; }
    [BindProperty] public Guid Id { get; set; }
    [BindProperty] public int? Revision { get; set; }
    [BindProperty] public Guid UnitId { get; set; }
    [BindProperty] public DateOnly Arrival { get; set; }
    [BindProperty] public DateOnly Departure { get; set; }
    [BindProperty] public int Guests { get; set; } = 1;
    [BindProperty] public string GuestName { get; set; } = "";
    [BindProperty] public string Phone { get; set; } = "";
    [BindProperty] public decimal? Amount { get; set; }
    [BindProperty] public bool Agreed { get; set; }
    [BindProperty] public bool Reviewed { get; set; }
    private Guid Owner => Guid.Parse(User.FindFirstValue("staywidget:user")!);
    private async Task<Property?> Load()
    {
        var p = await db.Properties.AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == Owner);
        if (p != null) Units = await db.Units.AsNoTracking().Where(x => x.PropertyId == p.Id).OrderBy(x => x.Name).ToListAsync();
        return p;
    }
    public async Task<IActionResult> OnGet(Guid? id)
    {
        var p = await Load(); if (p == null) return RedirectToPage("Index");
        if (id.HasValue)
        {
            var r = await db.Reservations.AsNoTracking().SingleOrDefaultAsync(x => x.PropertyId == p.Id && x.Id == id);
            if (r == null) return NotFound();
            Id = r.Id; Revision = r.Revision; State = r.State; UnitId = r.UnitId; Arrival = r.Arrival; Departure = r.Departure;
            Guests = r.Guests; GuestName = r.Name; Phone = r.Phone; Amount = r.Amount;
            CancellationRequested = r.CancellationRequested;
        }
        else { Id = Guid.NewGuid(); UnitId = Units[0].Id; Arrival = StayRules.Today(clock.GetUtcNow(), p.Zone).AddDays(7); Departure = Arrival.AddDays(2); }
        return Page();
    }
    public async Task<IActionResult> OnPost()
    {
        try
        {
            if (!ModelState.IsValid || !Amount.HasValue || !decimal.TryParse(Request.Form[nameof(Amount)], System.Globalization.NumberStyles.AllowDecimalPoint,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed) || parsed != Amount)
                throw new RuleViolation("Check dates, guest details and agreed total. Use a plain amount with up to two decimals.");
            var input = new ManualStay(UnitId, Arrival, Departure, Guests, GuestName, Phone, Amount.Value, Agreed);
            if (Revision.HasValue) await service.EditReservation(Owner, Id, Revision.Value, input);
            else await service.ManualBooking(Owner, Id, input);
            return RedirectToPage("Inventory", new { From = Arrival.ToString("yyyy-MM-dd") });
        }
        catch (RuleViolation e)
        {
            Response.StatusCode = 409; ModelState.AddModelError("", e.Message); await Load();
            State = Revision.HasValue ? "Confirmed" : "New"; return Page();
        }
    }
    public async Task<IActionResult> OnPostCancel()
    {
        ModelState.Clear();
        try
        {
            if (!Revision.HasValue) throw new RuleViolation("Reservation not found.");
            await service.CancelReservation(Owner, Id, Revision.Value, Reviewed);
            return RedirectToPage(new { id = Id });
        }
        catch (RuleViolation e)
        {
            await OnGet(Id); Response.StatusCode = 409; ModelState.AddModelError("", e.Message); return Page();
        }
    }
    public async Task<IActionResult> OnPostKeep()
    {
        ModelState.Clear();
        try { if(!Revision.HasValue || !Reviewed)throw new RuleViolation("Review the cancellation request and contact the guest before keeping the booking.");await service.KeepReservation(Owner,Id,Revision.Value);return RedirectToPage(new{id=Id}); }
        catch(RuleViolation e){await OnGet(Id);Response.StatusCode=409;ModelState.AddModelError("",e.Message);return Page();}
    }
}
