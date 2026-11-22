using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QuranAnchor.Application.Abstractions;
using QuranAnchor.Domain.Entities;
using QuranAnchor.Domain.Enums;
using QuranAnchor.Infrastructure.Persistence;

namespace QuranAnchor.Infrastructure.Services;

/// <summary>
/// Monthly portal fee through Paystack (naira) and Flutterwave (every other currency). The server sets the amount,
/// and only the provider's own confirmation (a verify call, or a signed webhook) is trusted. Each payment reference
/// is unique, so a repeated message never credits twice.
/// </summary>
public class PaymentService(
    QuranAnchorDbContext db,
    HttpClient http,
    IConfiguration config,
    INotificationService notify,
    ISettingsService settings,
    ILogger<PaymentService> logger) : IPaymentService
{
    private const string FlutterwaveApi = "https://api.flutterwave.com/v3/";

    private string PaystackKey => config["Paystack:SecretKey"] ?? string.Empty;
    private string FlutterwaveKey => config["Flutterwave:SecretKey"] ?? string.Empty;
    private string FlutterwaveHash => config["Flutterwave:SecretHash"] ?? string.Empty;

    private static long ToMinorUnits(decimal amount) => (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);

    /// <summary>Naira goes through Paystack. Every other currency goes through Flutterwave when it is set up.</summary>
    private string? ProviderFor(string currency)
    {
        var paystack = !string.IsNullOrWhiteSpace(PaystackKey);
        var flutterwave = !string.IsNullOrWhiteSpace(FlutterwaveKey);
        if (currency == "NGN") return paystack ? "Paystack" : flutterwave ? "Flutterwave" : null;
        return flutterwave ? "Flutterwave" : paystack ? "Paystack" : null;
    }

    private static string? ReadMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("message", out var m) ? m.GetString() : null;
        }
        catch (JsonException) { return null; }
    }

    private static string FriendlyFailure(string? providerMessage) =>
        providerMessage is not null && providerMessage.Contains("currenc", StringComparison.OrdinalIgnoreCase)
            ? "This currency cannot be used for payment right now. Please choose another currency, or contact the supervisor."
            : "The payment page could not be opened. Please try again, or choose another currency.";

    public async Task<(bool Ok, string? Url, string? Error)> StartCheckoutAsync(Guid studentId, string currency, string callbackUrl, string? flutterwaveCallbackUrl = null, CancellationToken ct = default)
    {
        currency = (currency ?? string.Empty).Trim().ToUpperInvariant();
        var provider = ProviderFor(currency);
        if (provider is null) return (false, null, "Online payment is not set up yet. Please contact the supervisor.");

        var student = await db.Students.FirstOrDefaultAsync(s => s.Id == studentId, ct);
        if (student is null) return (false, null, "Student not found.");

        var price = await db.PortalPrices.AsNoTracking().FirstOrDefaultAsync(p => p.Currency == currency && p.IsActive, ct);
        if (price is null) return (false, null, "That currency is not available.");

        var payment = new Payment
        {
            StudentId = student.Id,
            Provider = provider,
            Reference = "QA" + Guid.NewGuid().ToString("N")[..20],
            Currency = currency,
            Amount = price.Amount
        };
        db.Payments.Add(payment);
        student.PreferredCurrency = currency;
        await db.SaveChangesAsync(ct);

        return provider == "Flutterwave"
            ? await StartFlutterwaveAsync(payment, student, flutterwaveCallbackUrl ?? callbackUrl, ct)
            : await StartPaystackAsync(payment, student, callbackUrl, ct);
    }

    // ---------- Paystack ----------

    private async Task<(bool Ok, string? Url, string? Error)> StartPaystackAsync(Payment payment, StudentProfile student, string callbackUrl, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "transaction/initialize");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PaystackKey);
        request.Content = JsonContent.Create(new
        {
            email = student.ParentEmail,
            amount = ToMinorUnits(payment.Amount),
            currency = payment.Currency,
            reference = payment.Reference,
            callback_url = callbackUrl,
            metadata = new { student_code = student.StudentCode }
        });

        try
        {
            using var response = await http.SendAsync(request, ct);
            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            if (response.IsSuccessStatusCode && doc.RootElement.GetProperty("status").GetBoolean())
            {
                var url = doc.RootElement.GetProperty("data").GetProperty("authorization_url").GetString();
                return (true, url, null);
            }

            var reason = ReadMessage(json);
            logger.LogError("Paystack initialize failed for {Currency}: {Reason} | {Body}", payment.Currency, reason, json);
            payment.Status = PaymentStatus.Failed;
            payment.Note = Truncate("Paystack: " + reason);
            await db.SaveChangesAsync(ct);
            return (false, null, FriendlyFailure(reason));
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or KeyNotFoundException)
        {
            logger.LogError(ex, "Paystack initialize error");
            return (false, null, "The payment service is not reachable. Please try again.");
        }
    }

    public async Task<(bool Ok, string Message)> VerifyAsync(string reference, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(PaystackKey)) return (false, "Online payment is not set up.");

        using var request = new HttpRequestMessage(HttpMethod.Get, $"transaction/verify/{Uri.EscapeDataString(reference)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", PaystackKey);

        try
        {
            using var response = await http.SendAsync(request, ct);
            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var data = doc.RootElement.GetProperty("data");
            if (data.GetProperty("status").GetString() != "success") return (false, "The payment was not completed.");

            var ok = await ApplySuccessAsync("Paystack", reference, data.GetProperty("amount").GetInt64(), data.GetProperty("currency").GetString() ?? "", ct);
            return ok ? (true, "Payment received. Thank you.") : (false, "The payment could not be matched. Please contact the supervisor.");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or KeyNotFoundException)
        {
            logger.LogError(ex, "Paystack verify error");
            return (false, "We could not confirm the payment yet. If you were charged, it will appear shortly.");
        }
    }

    public async Task<bool> HandleWebhookAsync(string body, string signature, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(PaystackKey) || string.IsNullOrWhiteSpace(signature)) return false;

        var expected = Convert.ToHexString(HMACSHA512.HashData(Encoding.UTF8.GetBytes(PaystackKey), Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
        var given = signature.Trim().ToLowerInvariant();
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(given))) return false;

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.GetProperty("event").GetString() != "charge.success") return true;

            var data = doc.RootElement.GetProperty("data");
            await ApplySuccessAsync("Paystack", data.GetProperty("reference").GetString() ?? "", data.GetProperty("amount").GetInt64(), data.GetProperty("currency").GetString() ?? "", ct);
            return true;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException)
        {
            logger.LogError(ex, "Paystack webhook could not be read");
            return true;   // signature was valid; do not make Paystack retry a message we cannot read
        }
    }

    // ---------- Flutterwave ----------

    private async Task<(bool Ok, string? Url, string? Error)> StartFlutterwaveAsync(Payment payment, StudentProfile student, string redirectUrl, CancellationToken ct)
    {
        var brand = await settings.GetBrandingAsync(ct);
        using var request = new HttpRequestMessage(HttpMethod.Post, FlutterwaveApi + "payments");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", FlutterwaveKey);
        request.Content = JsonContent.Create(new
        {
            tx_ref = payment.Reference,
            amount = payment.Amount,
            currency = payment.Currency,
            redirect_url = redirectUrl,
            customer = new { email = student.ParentEmail, name = string.IsNullOrWhiteSpace(student.ParentName) ? student.FullName : student.ParentName },
            customizations = new { title = brand.AcademyName, description = "Portal fee for " + student.FullName },
            meta = new { student_code = student.StudentCode }
        });

        try
        {
            using var response = await http.SendAsync(request, ct);
            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            if (response.IsSuccessStatusCode && doc.RootElement.GetProperty("status").GetString() == "success")
            {
                var link = doc.RootElement.GetProperty("data").GetProperty("link").GetString();
                if (!string.IsNullOrWhiteSpace(link)) return (true, link, null);
            }

            var reason = ReadMessage(json);
            logger.LogError("Flutterwave initialize failed for {Currency}: {Reason} | {Body}", payment.Currency, reason, json);
            payment.Status = PaymentStatus.Failed;
            payment.Note = Truncate("Flutterwave: " + reason);
            await db.SaveChangesAsync(ct);
            return (false, null, FriendlyFailure(reason));
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or KeyNotFoundException)
        {
            logger.LogError(ex, "Flutterwave initialize error");
            return (false, null, "The payment service is not reachable. Please try again.");
        }
    }

    public async Task<(bool Ok, string Message)> VerifyFlutterwaveAsync(string txRef, string? transactionId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(FlutterwaveKey)) return (false, "Online payment is not set up.");
        if (!long.TryParse(transactionId, NumberStyles.None, CultureInfo.InvariantCulture, out var id)) return (false, "The payment was not completed.");

        using var request = new HttpRequestMessage(HttpMethod.Get, FlutterwaveApi + $"transactions/{id}/verify");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", FlutterwaveKey);

        try
        {
            using var response = await http.SendAsync(request, ct);
            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            if (!response.IsSuccessStatusCode || doc.RootElement.GetProperty("status").GetString() != "success") return (false, "We could not confirm the payment yet.");

            var data = doc.RootElement.GetProperty("data");
            if (data.GetProperty("status").GetString() != "successful") return (false, "The payment was not completed.");
            // The confirmation must be for the reference we asked about, not another payment.
            if (!string.Equals(data.GetProperty("tx_ref").GetString(), txRef, StringComparison.Ordinal)) return (false, "The payment could not be matched. Please contact the supervisor.");

            var amount = data.GetProperty("amount").GetDecimal();
            var ok = await ApplySuccessAsync("Flutterwave", txRef, ToMinorUnits(amount), data.GetProperty("currency").GetString() ?? "", ct);
            return ok ? (true, "Payment received. Thank you.") : (false, "The payment could not be matched. Please contact the supervisor.");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            logger.LogError(ex, "Flutterwave verify error");
            return (false, "We could not confirm the payment yet. If you were charged, it will appear shortly.");
        }
    }

    /// <summary>Flutterwave sends a secret hash you set in its dashboard. The message is trusted only if it matches, and the payment is then re-checked with Flutterwave.</summary>
    public async Task<bool> HandleFlutterwaveWebhookAsync(string body, string verifHash, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(FlutterwaveHash) || string.IsNullOrWhiteSpace(verifHash)) return false;
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(FlutterwaveHash), Encoding.UTF8.GetBytes(verifHash.Trim()))) return false;

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("data", out var data)) return true;
            if (!data.TryGetProperty("id", out var idElement) || !data.TryGetProperty("tx_ref", out var refElement)) return true;

            var id = idElement.ValueKind == JsonValueKind.Number ? idElement.GetInt64().ToString(CultureInfo.InvariantCulture) : idElement.GetString();
            await VerifyFlutterwaveAsync(refElement.GetString() ?? "", id, ct);
            return true;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            logger.LogError(ex, "Flutterwave webhook could not be read");
            return true;
        }
    }

    // ---------- Shared ----------

    private static string Truncate(string text) => text.Length <= 480 ? text : text[..480];

    /// <summary>Marks a payment as paid only if the provider's amount and currency match what we asked for.</summary>
    private async Task<bool> ApplySuccessAsync(string provider, string reference, long amountMinor, string currency, CancellationToken ct)
    {
        var payment = await db.Payments.Include(p => p.Student).FirstOrDefaultAsync(p => p.Reference == reference, ct);
        if (payment?.Student is null) return false;
        if (!string.Equals(payment.Provider, provider, StringComparison.Ordinal)) return false;
        if (payment.Status == PaymentStatus.Succeeded) return true;   // already counted

        if (!string.Equals(currency, payment.Currency, StringComparison.OrdinalIgnoreCase) || amountMinor != ToMinorUnits(payment.Amount))
        {
            logger.LogWarning("Payment {Reference} mismatch: got {Amount} {Currency}", reference, amountMinor, currency);
            payment.Status = PaymentStatus.Failed;
            payment.Note = "Amount or currency did not match";
            await db.SaveChangesAsync(ct);
            return false;
        }

        await ExtendAsync(payment, payment.Student, ct);
        return true;
    }

    public async Task MarkManualAsync(Guid studentId, string note, CancellationToken ct = default)
    {
        var student = await db.Students.FirstOrDefaultAsync(s => s.Id == studentId, ct);
        if (student is null) return;

        var price = await db.PortalPrices.AsNoTracking().FirstOrDefaultAsync(p => p.Currency == student.PreferredCurrency && p.IsActive, ct);
        var payment = new Payment
        {
            StudentId = student.Id,
            Provider = "Manual",
            Reference = "MANUAL" + Guid.NewGuid().ToString("N")[..16],
            Currency = student.PreferredCurrency,
            Amount = price?.Amount ?? 0,
            IsManual = true,
            Note = note.Trim()
        };
        db.Payments.Add(payment);
        await ExtendAsync(payment, student, ct);
    }

    private async Task ExtendAsync(Payment payment, StudentProfile student, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var start = student.PaidUntilUtc is { } paid && paid > now ? paid : now;
        payment.Status = PaymentStatus.Succeeded;
        payment.PaidAtUtc = now;
        payment.PeriodStartUtc = start;
        payment.PeriodEndUtc = start.AddMonths(1);
        student.PaidUntilUtc = payment.PeriodEndUtc;
        await db.SaveChangesAsync(ct);

        var body = await notify.WrapAsync("Payment received",
            $"Thank you. We received the portal fee of <strong>{payment.Currency} {payment.Amount:N2}</strong> for {System.Net.WebUtility.HtmlEncode(student.FullName)}.",
            $"Your access is paid until <strong>{payment.PeriodEndUtc:d MMM yyyy}</strong>.",
            $"Reference: {payment.Reference}");
        await notify.SendToParentAsync(student, "Portal fee receipt", body, ct);
    }
}
