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
/// Monthly portal fee through Paystack. The server sets the amount, and only Paystack's own confirmation
/// (webhook or verify call) is trusted. Each payment reference is unique, so a repeated message never credits twice.
/// </summary>
public class PaymentService(
    QuranAnchorDbContext db,
    HttpClient http,
    IConfiguration config,
    INotificationService notify,
    ILogger<PaymentService> logger) : IPaymentService
{
    private string SecretKey => config["Paystack:SecretKey"] ?? string.Empty;

    private static long ToMinorUnits(decimal amount) => (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);

    public async Task<(bool Ok, string? Url, string? Error)> StartCheckoutAsync(Guid studentId, string currency, string callbackUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(SecretKey)) return (false, null, "Online payment is not set up yet. Please contact the supervisor.");

        var student = await db.Students.FirstOrDefaultAsync(s => s.Id == studentId, ct);
        if (student is null) return (false, null, "Student not found.");

        currency = currency.ToUpperInvariant();
        var price = await db.PortalPrices.AsNoTracking().FirstOrDefaultAsync(p => p.Currency == currency && p.IsActive, ct);
        if (price is null) return (false, null, "That currency is not available.");

        var payment = new Payment
        {
            StudentId = student.Id,
            Reference = "QA" + Guid.NewGuid().ToString("N")[..20],
            Currency = currency,
            Amount = price.Amount
        };
        db.Payments.Add(payment);
        student.PreferredCurrency = currency;
        await db.SaveChangesAsync(ct);

        using var request = new HttpRequestMessage(HttpMethod.Post, "transaction/initialize");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", SecretKey);
        request.Content = JsonContent.Create(new
        {
            email = student.ParentEmail,
            amount = ToMinorUnits(price.Amount),
            currency,
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

            logger.LogError("Paystack initialize failed: {Body}", json);
            payment.Status = PaymentStatus.Failed;
            await db.SaveChangesAsync(ct);
            return (false, null, "The payment page could not be opened. Please try again.");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or KeyNotFoundException)
        {
            logger.LogError(ex, "Paystack initialize error");
            return (false, null, "The payment service is not reachable. Please try again.");
        }
    }

    public async Task<(bool Ok, string Message)> VerifyAsync(string reference, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(SecretKey)) return (false, "Online payment is not set up.");

        using var request = new HttpRequestMessage(HttpMethod.Get, $"transaction/verify/{Uri.EscapeDataString(reference)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", SecretKey);

        try
        {
            using var response = await http.SendAsync(request, ct);
            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var data = doc.RootElement.GetProperty("data");
            if (data.GetProperty("status").GetString() != "success") return (false, "The payment was not completed.");

            var ok = await ApplySuccessAsync(reference, data.GetProperty("amount").GetInt64(), data.GetProperty("currency").GetString() ?? "", ct);
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
        if (string.IsNullOrWhiteSpace(SecretKey) || string.IsNullOrWhiteSpace(signature)) return false;

        var expected = Convert.ToHexString(HMACSHA512.HashData(Encoding.UTF8.GetBytes(SecretKey), Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
        var given = signature.Trim().ToLowerInvariant();
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(given))) return false;

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.GetProperty("event").GetString() != "charge.success") return true;

            var data = doc.RootElement.GetProperty("data");
            await ApplySuccessAsync(data.GetProperty("reference").GetString() ?? "", data.GetProperty("amount").GetInt64(), data.GetProperty("currency").GetString() ?? "", ct);
            return true;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException)
        {
            logger.LogError(ex, "Paystack webhook could not be read");
            return true;   // signature was valid; do not make Paystack retry a message we cannot read
        }
    }

    /// <summary>Marks a payment as paid only if Paystack's amount and currency match what we asked for.</summary>
    private async Task<bool> ApplySuccessAsync(string reference, long amountMinor, string currency, CancellationToken ct)
    {
        var payment = await db.Payments.Include(p => p.Student).FirstOrDefaultAsync(p => p.Reference == reference, ct);
        if (payment?.Student is null) return false;
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
