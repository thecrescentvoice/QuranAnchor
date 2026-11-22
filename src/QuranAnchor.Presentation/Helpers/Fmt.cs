using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Html;
using QuranAnchor.Application.Common;
using QuranAnchor.Application.Models;

namespace QuranAnchor.Presentation.Helpers;

public static class Fmt
{
    private static readonly Regex UrlPattern = new(@"https?://(?:[^\s<&]|&amp;)+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex VideoPattern = new(@"https?://(?:www\.|m\.)?(?:youtube\.com/(?:watch\?|shorts/|live/)|youtu\.be/)[^\s<>""]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string Shifts(QuranAnchor.Domain.Enums.TeachingShift shifts)
    {
        var parts = new List<string>();
        if (shifts.HasFlag(QuranAnchor.Domain.Enums.TeachingShift.Daytime)) parts.Add("Daytime");
        if (shifts.HasFlag(QuranAnchor.Domain.Enums.TeachingShift.PrimeEvening)) parts.Add("Prime evening");
        if (shifts.HasFlag(QuranAnchor.Domain.Enums.TeachingShift.NightOverseas)) parts.Add("Night overseas");
        return parts.Count == 0 ? "None" : string.Join(", ", parts);
    }

    /// <summary>Makes the text safe, then turns web addresses (http or https only) into links that open in a new tab.</summary>
    public static string LinkifyHtml(string? text, bool lineBreaks = false)
    {
        var encoded = WebUtility.HtmlEncode(text ?? string.Empty);
        var linked = UrlPattern.Replace(encoded, m =>
        {
            var url = m.Value;
            var trailing = string.Empty;
            while (url.Length > 0 && ".,;:!?)]".Contains(url[^1])) { trailing = url[^1] + trailing; url = url[..^1]; }
            return url.Length == 0 ? m.Value : $"<a href=\"{url}\" target=\"_blank\" rel=\"noopener noreferrer\">{url}</a>{trailing}";
        });
        return lineBreaks ? linked.Replace("\r\n", "\n").Replace("\n", "<br />") : linked;
    }

    public static IHtmlContent Linkify(string? text) => new HtmlString(LinkifyHtml(text));

    /// <summary>The first YouTube address in the text, if any, for a "Watch the video" button.</summary>
    public static string? VideoLink(string? text)
    {
        var m = VideoPattern.Match(text ?? string.Empty);
        return m.Success ? m.Value.TrimEnd('.', ',', ';', ')', '!', '?') : null;
    }

    public static string Local(DateTime utc, string tz) => TimeHelper.Format(utc, tz);
    public static string Day(DateTime utc, string tz) => TimeHelper.ToLocal(utc, tz).ToString("ddd d MMM");
    public static string Time(DateTime utc, string tz) => TimeHelper.ToLocal(utc, tz).ToString("h:mm tt");
    private static readonly string[] HijriMonths =
    {
        "Muharram", "Safar", "Rabi’ al-Awwal", "Rabi’ al-Thani", "Jumada al-Ula", "Jumada al-Akhirah",
        "Rajab", "Sha’ban", "Ramadan", "Shawwal", "Dhu al-Qi’dah", "Dhu al-Hijjah"
    };

    /// <summary>Today's Hijri date (Umm al-Qura calendar). The month number is returned too, so 9 can show "Ramadan Mubarak".</summary>
    public static string Hijri(DateTime date, out int month)
    {
        var cal = new System.Globalization.UmAlQuraCalendar();
        month = cal.GetMonth(date);
        return $"{cal.GetDayOfMonth(date)} {HijriMonths[month - 1]} {cal.GetYear(date)} AH";
    }

    public static string Initials(string? name)
    {
        var parts = (name ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? "?" : (parts[0][..1] + (parts.Length > 1 ? parts[^1][..1] : "")).ToUpperInvariant();
    }

    public static string Month(int year, int month) => new DateTime(year, month, 1).ToString("MMMM yyyy");
    public static string Pct(double? v) => v is null ? "-" : $"{v:0}%";
    public static string Money(string currency, decimal amount) =>
        currency switch { "NGN" => "₦", "USD" => "$", "GBP" => "£", _ => currency + " " } + amount.ToString("N2");

    public static string WhatsApp(BrandingSettings b, string message) =>
        string.IsNullOrEmpty(b.SupervisorWhatsApp) ? "" : $"https://wa.me/{b.SupervisorWhatsApp}?text={Uri.EscapeDataString(message)}";
}
