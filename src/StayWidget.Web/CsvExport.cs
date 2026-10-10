using System.Globalization;
using System.Text;
using StayWidget.Persistence;
namespace StayWidget.Web;

public static class CsvExport
{
    public static string Cell(string? value)
    {
        value ??= "";
        // Quote every field; neutralize formula prefixes even after spaces/control characters.
        var trimmed = value.TrimStart();
        if (trimmed.Length > 0 && ("=+-@".Contains(trimmed[0]) || value.Any(c => c is '\t' or '\r' or '\n')))
            value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
    public static byte[] Reservations(IEnumerable<Reservation> reservations, IReadOnlyDictionary<Guid, string> units)
    {
        var s = new StringBuilder("Unit,State,Arrival,Departure,Guests,Name,Phone,AgreedTotal,Currency\r\n");
        foreach (var r in reservations)
            s.AppendLine(string.Join(",", new[] { units[r.UnitId], r.State, r.Arrival.ToString("yyyy-MM-dd"), r.Departure.ToString("yyyy-MM-dd"),
                r.Guests.ToString(CultureInfo.InvariantCulture), r.Name, r.Phone, r.Amount.ToString("0.00", CultureInfo.InvariantCulture), r.Currency }.Select(Cell)));
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(s.ToString())).ToArray();
    }
}
