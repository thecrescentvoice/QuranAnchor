using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuranAnchor.Application.Abstractions;
using QuranAnchor.Domain.Constants;
using QuranAnchor.Domain.Entities;
using QuranAnchor.Domain.Enums;
using QuranAnchor.Infrastructure.Persistence;
using QuranAnchor.Presentation.Helpers;
using QuranAnchor.Presentation.Services;

namespace QuranAnchor.Presentation.Controllers;

[Authorize]
public class CertificatesController(
    QuranAnchorDbContext db,
    IPdfService pdf,
    INotificationService notify,
    ISettingsService settings,
    IAuditLogger audit,
    CurrentUser me) : Controller
{
    private async Task<bool> CanSeeAsync(Certificate c)
    {
        if (me.IsStaff) return true;
        if (me.IsStudent) return (await me.StudentAsync())?.Id == c.StudentId;
        var tutor = await me.TutorAsync();
        return tutor is not null && await db.Students.AnyAsync(s => s.Id == c.StudentId && s.TutorId == tutor.Id);
    }

    public async Task<IActionResult> Index(int page = 1)
    {
        var query = db.Certificates.AsNoTracking().Include(c => c.Student).AsQueryable();
        if (me.IsStudent)
        {
            var student = await me.StudentAsync();
            query = query.Where(c => c.StudentId == student!.Id && !c.IsRevoked);
        }
        else if (me.IsTutor)
        {
            var tutor = await me.TutorAsync();
            query = query.Where(c => c.Student!.TutorId == tutor!.Id);
        }

        var (items, info) = await query.OrderByDescending(c => c.IssuedOn).ThenByDescending(c => c.CreatedAtUtc).PageAsync(page);
        ViewBag.PageInfo = info;
        return View(items);
    }

    // ---------- Issue ----------

    private async Task FillStudentsAsync()
    {
        var query = db.Students.AsNoTracking().Where(s => s.Status != AccountStatus.Deactivated);
        if (me.IsTutor)
        {
            var tutor = await me.TutorAsync();
            query = query.Where(s => s.TutorId == tutor!.Id);
        }
        ViewBag.Students = (await query.OrderBy(s => s.FullName).ToListAsync())
            .Select(s => new SelectListItem($"{s.FullName} ({s.StudentCode})", s.Id.ToString())).ToList();
    }

    [Authorize(Policy = Permissions.IssueCertificates)]
    [HttpGet]
    public async Task<IActionResult> Create(Guid? studentId)
    {
        await FillStudentsAsync();
        ViewBag.StudentId = studentId;
        return View();
    }

    [Authorize(Policy = Permissions.IssueCertificates)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Guid studentId, string title, string? description, DateOnly issuedOn)
    {
        var student = await db.Students.FirstOrDefaultAsync(s => s.Id == studentId);
        if (student is null || string.IsNullOrWhiteSpace(title))
        {
            TempData["Warning"] = "Choose a student and write what the certificate is for.";
            return RedirectToAction(nameof(Create), new { studentId });
        }
        if (me.IsTutor && student.TutorId != (await me.TutorAsync())?.Id) return Forbid();

        var brand = await settings.GetBrandingAsync();
        var cert = new Certificate
        {
            StudentId = studentId,
            Code = $"{brand.IdPrefix}-CERT-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            Title = title.Trim(), Description = description?.Trim() ?? string.Empty, IssuedOn = issuedOn,
            IssuedByName = User.FindFirst(AppClaimTypes.FullName)?.Value ?? User.Identity?.Name ?? "",
            IssuedByUserId = me.UserId
        };
        db.Certificates.Add(cert);
        await db.SaveChangesAsync();
        await audit.LogAsync("CertificateIssued", cert.Code, student.StudentCode);

        var body = await notify.WrapAsync("A certificate was awarded",
            $"Congratulations! {WebUtility.HtmlEncode(student.FullName)} received a certificate: <strong>{WebUtility.HtmlEncode(cert.Title)}</strong>.",
            "Sign in to the portal to see it and download it as a PDF.");
        await notify.SendToParentAsync(student, "A certificate was awarded", body);

        TempData["Saved"] = "Certificate issued. The parent was emailed.";
        return RedirectToAction(nameof(Show), new { id = cert.Id });
    }

    // ---------- View, download, withdraw ----------

    public async Task<IActionResult> Show(Guid id)
    {
        var cert = await db.Certificates.AsNoTracking().Include(c => c.Student).FirstOrDefaultAsync(c => c.Id == id);
        if (cert is null || !await CanSeeAsync(cert)) return NotFound();
        if (me.IsStudent && cert.IsRevoked) return NotFound();
        return View(cert);
    }

    public async Task<IActionResult> Pdf(Guid id)
    {
        var cert = await db.Certificates.AsNoTracking().Include(c => c.Student).FirstOrDefaultAsync(c => c.Id == id);
        if (cert is null || !await CanSeeAsync(cert) || cert.IsRevoked) return NotFound();
        return File(await pdf.CertificatePdfAsync(id), "application/pdf", $"Certificate-{cert.Code}.pdf");
    }

    [Authorize(Policy = Permissions.ManageSchedules)]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Revoke(Guid id)
    {
        var cert = await db.Certificates.FirstOrDefaultAsync(c => c.Id == id);
        if (cert is null) return NotFound();
        cert.IsRevoked = !cert.IsRevoked;
        await db.SaveChangesAsync();
        await audit.LogAsync(cert.IsRevoked ? "CertificateWithdrawn" : "CertificateRestored", cert.Code);
        TempData["Saved"] = cert.IsRevoked ? "Certificate withdrawn. It no longer verifies." : "Certificate restored.";
        return RedirectToAction(nameof(Show), new { id });
    }

    /// <summary>Public page: anyone with the code can check a certificate is genuine. It shows only the first name and the achievement.</summary>
    [AllowAnonymous]
    public async Task<IActionResult> Verify(string id)
    {
        var cert = await db.Certificates.AsNoTracking().Include(c => c.Student).FirstOrDefaultAsync(c => c.Code == id);
        ViewBag.Brand = await settings.GetBrandingAsync();
        return View(cert);
    }
}
