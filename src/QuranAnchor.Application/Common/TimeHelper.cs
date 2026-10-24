namespace QuranAnchor.Application.Common;

/// <summary>Time zone helpers. Classes are stored in UTC and shown in each person's own time zone.</summary>
public static class TimeHelper
{
    public static TimeZoneInfo Zone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return TimeZoneInfo.Utc;
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.Utc; }
        catch (InvalidTimeZoneException) { return TimeZoneInfo.Utc; }
    }

    public static DateTime ToUtc(DateOnly date, TimeOnly time, string tz)
    {
        var zone = Zone(tz);
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local)) local = local.AddHours(1);   // clocks jumped forward
        return TimeZoneInfo.ConvertTimeToUtc(local, zone);
    }

    public static DateTime ToLocal(DateTime utc, string tz) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone(tz));

    public static DateOnly LocalToday(string tz) =>
        DateOnly.FromDateTime(ToLocal(DateTime.UtcNow, tz));

    public static string Format(DateTime utc, string tz) =>
        ToLocal(utc, tz).ToString("ddd d MMM yyyy, h:mm tt");

    /// <summary>Shows one moment in two time zones, for example "6:30 PM Lagos = 5:30 PM London".</summary>
    public static string Both(DateTime utc, string tzA, string tzB)
    {
        string Short(string tz) => ToLocal(utc, tz).ToString("h:mm tt") + " " + City(tz);
        return Short(tzA) + " = " + Short(tzB);
    }

    public static string City(string tz)
    {
        var i = tz.LastIndexOf('/');
        return (i >= 0 ? tz[(i + 1)..] : tz).Replace('_', ' ');
    }

    /// <summary>A short list of common time zones for the dropdowns.</summary>
    public static readonly string[] CommonZones =
    {
        "Africa/Lagos", "Africa/Accra", "Africa/Nairobi", "Africa/Johannesburg", "Africa/Cairo",
        "Europe/London", "Europe/Paris", "Europe/Berlin", "Europe/Istanbul",
        "Asia/Dubai", "Asia/Riyadh", "Asia/Karachi", "Asia/Kolkata", "Asia/Singapore", "Asia/Kuala_Lumpur",
        "Australia/Sydney",
        "America/New_York", "America/Chicago", "America/Denver", "America/Los_Angeles", "America/Toronto", "America/Sao_Paulo"
    };
}
