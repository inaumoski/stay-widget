namespace StayWidget.Domain;

public sealed class RuleViolation(string message) : Exception(message);
public static class StayRules
{
    public static void Validate(DateOnly arrival, DateOnly departure, int guests, int capacity,
        DateOnly today, DateOnly openFrom, DateOnly openTo, int min = 1, int max = 28)
    {
        var nights = departure.DayNumber - arrival.DayNumber;
        if (arrival <= today || arrival > today.AddDays(365)) throw new RuleViolation("Choose an arrival from tomorrow through the next 365 days.");
        if (nights < min || nights > max) throw new RuleViolation($"Choose a stay of {min}–{max} nights.");
        if (guests < 1 || guests > capacity) throw new RuleViolation("Guest count exceeds this unit's capacity.");
        if (arrival < openFrom || departure > openTo) throw new RuleViolation("These dates are outside the unit's open season.");
    }
    public static DateOnly Today(DateTimeOffset now, string zone) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(zone)).DateTime);
    public static DateTimeOffset Deadline(DateTimeOffset now, DateOnly arrival, string zone)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(zone);
        var local = arrival.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        while (tz.IsInvalidTime(local)) local = local.AddMinutes(1);
        var offset = tz.IsAmbiguousTime(local) ? tz.GetAmbiguousTimeOffsets(local).Max() : tz.GetUtcOffset(local);
        var boundary = new DateTimeOffset(local, offset).ToUniversalTime();
        return boundary < now.AddHours(48) ? boundary : now.AddHours(48);
    }
    public static bool Overlaps(DateOnly a, DateOnly b, DateOnly c, DateOnly d) => a < d && c < b;
}
