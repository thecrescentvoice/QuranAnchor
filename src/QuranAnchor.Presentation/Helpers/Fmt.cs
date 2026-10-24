using QuranAnchor.Application.Common;
using QuranAnchor.Application.Models;

namespace QuranAnchor.Presentation.Helpers;

public static class Fmt
{
    public static string Local(DateTime utc, string tz) => TimeHelper.Format(utc, tz);
    public static string Day(DateTime utc, string tz) => TimeHelper.ToLocal(utc, tz).ToString("ddd d MMM");
    public static string Time(DateTime utc, string tz) => TimeHelper.ToLocal(utc, tz).ToString("h:mm tt");
    public static string Month(int year, int month) => new DateTime(year, month, 1).ToString("MMMM yyyy");
    public static string Pct(double? v) => v is null ? "-" : $"{v:0}%";
    public static string Money(string currency, decimal amount) =>
        currency switch { "NGN" => "₦", "USD" => "$", "GBP" => "£", _ => currency + " " } + amount.ToString("N2");

    public static string WhatsApp(BrandingSettings b, string message) =>
        string.IsNullOrEmpty(b.SupervisorWhatsApp) ? "" : $"https://wa.me/{b.SupervisorWhatsApp}?text={Uri.EscapeDataString(message)}";
}
