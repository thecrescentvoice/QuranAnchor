using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuranAnchor.Application.Abstractions;
using QuranAnchor.Domain.Constants;
using QuranAnchor.Domain.Entities;
using QuranAnchor.Infrastructure.Persistence;
using QuranAnchor.Presentation.ViewModels;

namespace QuranAnchor.Presentation.Controllers;

/// <summary>The monthly portal fee in each currency, and what a tutor earns per class length. Super Admin only.</summary>
[Authorize(Policy = Permissions.EditPricing)]
public class PricingController(QuranAnchorDbContext db, IAuditLogger audit) : Controller
{
    private static readonly string[] Currencies = { "NGN", "USD", "GBP" };

    private static bool TryAmount(string? text, out decimal amount) =>
        decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out amount) && amount >= 0;

    public async Task<IActionResult> Index()
    {
        var prices = await db.PortalPrices.ToListAsync();
        foreach (var c in Currencies.Where(c => prices.All(p => p.Currency != c)))
        {
            var row = new PortalPrice { Currency = c, Amount = 0, IsActive = false };
            db.PortalPrices.Add(row);
            prices.Add(row);
        }
        await db.SaveChangesAsync();

        return View(new PricingViewModel
        {
            Prices = prices.OrderBy(p => { var i = Array.IndexOf(Currencies, p.Currency); return i < 0 ? 99 : i; }).ThenBy(p => p.Currency).ToList(),
            Rates = await db.SessionRates.AsNoTracking().OrderBy(r => r.DurationMinutes).ToListAsync()
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePrices()
    {
        var changes = new List<string>();
        foreach (var row in await db.PortalPrices.ToListAsync())
        {
            if (!TryAmount(Request.Form[$"price_{row.Currency}"], out var amount)) continue;
            var active = Request.Form[$"active_{row.Currency}"].Contains("true");
            if (row.Amount != amount || row.IsActive != active) changes.Add($"{row.Currency} {row.Amount:0.00}->{amount:0.00}{(active ? "" : " (off)")}");
            row.Amount = amount;
            row.IsActive = active;
        }
        await db.SaveChangesAsync();
        await audit.LogAsync("PortalPricesChanged", "Pricing", string.Join("; ", changes));
        TempData["Saved"] = "Prices saved. They apply to new payments.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Adds any currency (a three-letter code such as EUR, CAD or AED) with its monthly portal fee.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCurrency(string code, string amount)
    {
        code = (code ?? string.Empty).Trim().ToUpperInvariant();
        if (code.Length != 3 || !code.All(c => c is >= 'A' and <= 'Z') || !TryAmount(amount, out var value) || value <= 0)
        {
            TempData["Warning"] = "Enter a three-letter currency code, such as EUR, and an amount greater than zero.";
            return RedirectToAction(nameof(Index));
        }
        if (await db.PortalPrices.AnyAsync(p => p.Currency == code))
        {
            TempData["Warning"] = $"{code} is already in the list. Change its amount above instead.";
            return RedirectToAction(nameof(Index));
        }

        db.PortalPrices.Add(new PortalPrice { Currency = code, Amount = value, IsActive = true });
        await db.SaveChangesAsync();
        await audit.LogAsync("PortalCurrencyAdded", code, value.ToString("0.00", CultureInfo.InvariantCulture));
        TempData["Saved"] = $"{code} added. Parents can now pay in it, if Flutterwave supports it.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveCurrency(string code)
    {
        if (Currencies.Contains(code)) return RedirectToAction(nameof(Index));
        var row = await db.PortalPrices.FirstOrDefaultAsync(p => p.Currency == code);
        if (row is null) return RedirectToAction(nameof(Index));
        db.PortalPrices.Remove(row);
        await db.SaveChangesAsync();
        await audit.LogAsync("PortalCurrencyRemoved", code);
        TempData["Saved"] = $"{code} removed.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddRate(int duration, string amount)
    {
        if (duration < 5 || duration > 240 || !TryAmount(amount, out var value))
        {
            TempData["Warning"] = "Enter a class length in minutes and an amount.";
            return RedirectToAction(nameof(Index));
        }
        if (await db.SessionRates.AnyAsync(r => r.DurationMinutes == duration && r.TutorId == null))
        {
            TempData["Warning"] = "A rate for that length already exists. Change it instead.";
            return RedirectToAction(nameof(Index));
        }

        db.SessionRates.Add(new SessionRate { DurationMinutes = duration, Amount = value });
        await db.SaveChangesAsync();
        await audit.LogAsync("SessionRateAdded", $"{duration} min", value.ToString("0.00", CultureInfo.InvariantCulture));
        TempData["Saved"] = "Rate added. It applies to classes held from now on; past classes keep the amount they earned.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveRate(Guid id, string amount)
    {
        var rate = await db.SessionRates.FirstOrDefaultAsync(r => r.Id == id);
        if (rate is null || !TryAmount(amount, out var value)) return RedirectToAction(nameof(Index));

        var old = rate.Amount;
        rate.Amount = value;
        await db.SaveChangesAsync();
        await audit.LogAsync("SessionRateChanged", $"{rate.DurationMinutes} min", $"{old:0.00}->{value:0.00}");
        TempData["Saved"] = "Rate saved. It applies to classes held from now on.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRate(Guid id)
    {
        var rate = await db.SessionRates.FirstOrDefaultAsync(r => r.Id == id);
        if (rate is null) return RedirectToAction(nameof(Index));
        db.SessionRates.Remove(rate);
        await db.SaveChangesAsync();
        await audit.LogAsync("SessionRateRemoved", $"{rate.DurationMinutes} min");
        TempData["Saved"] = "Rate removed.";
        return RedirectToAction(nameof(Index));
    }
}
