using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuranAnchor.Application.Abstractions;
using QuranAnchor.Application.Models;
using QuranAnchor.Domain.Entities;
using QuranAnchor.Domain.Enums;
using QuranAnchor.Infrastructure.Persistence;

namespace QuranAnchor.Infrastructure.Services;

/// <summary>Makes the downloadable PDFs for the monthly plan and the monthly report.</summary>
public class PdfService(
    QuranAnchorDbContext db,
    IReportService reports,
    ISettingsService settings,
    IHostEnvironment env,
    IConfiguration config) : IPdfService
{
    private const string Violet = "#8B5CFF";
    private const string Cyan = "#5CE1E6";
    private const string Magenta = "#D61F9F";
    private const string Ink = "#2F3B3E";
    private const string Soft = "#5B6B6F";
    private const string Line = "#E3E6EF";
    private const string Tint = "#F1F3FA";

    private static bool _fontsReady;
    private static bool _arabicReady;
    private static readonly object FontLock = new();

    /// <summary>The bismillah in Arabic when the Amiri font is installed. Without the font, a plain English line is printed instead.</summary>
    private static void Bismillah(IContainer container, float size)
    {
        if (_arabicReady)
            container.AlignCenter().Text("بِسْمِ ٱللَّهِ ٱلرَّحْمَٰنِ ٱلرَّحِيمِ").FontFamily("Amiri").FontSize(size);
        else
            container.AlignCenter().Text("In the name of Allah, the Most Gracious, the Most Merciful").Italic().FontSize(size / 2.4f);
    }

    private void EnsureFonts()
    {
        lock (FontLock)
        {
            if (_fontsReady) return;
            QuestPDF.Settings.License = LicenseType.Community;
            // A missing font must never stop a PDF from being made.
            QuestPDF.Settings.ThrowOnMissingFontFamilies = false;

            // Put Amiri-Regular.ttf (free from Google Fonts) in wwwroot/fonts so the Arabic bismillah prints correctly.
            var path = Path.Combine(env.ContentRootPath, "wwwroot", "fonts", "Amiri-Regular.ttf");
            if (File.Exists(path))
            {
                using var stream = File.OpenRead(path);
                QuestPDF.Drawing.FontManager.RegisterFontFromStream(stream);
                _arabicReady = true;
            }
            _fontsReady = true;
        }
    }

    private static string[] Lines(string? text) =>
        (text ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string MonthName(int year, int month) => new DateTime(year, month, 1).ToString("MMMM yyyy");

    private static void TopBand(IContainer c) =>
        c.Row(r =>
        {
            r.RelativeItem().Height(5).Background(Violet);
            r.RelativeItem().Height(5).Background(Cyan);
            r.RelativeItem().Height(5).Background(Magenta);
        });

    private static void Heading(ColumnDescriptor col, string text) =>
        col.Item().PaddingTop(6).Text(text).FontSize(12).SemiBold().FontColor(Violet);

    private static void Bullets(ColumnDescriptor col, IEnumerable<string> lines, bool numbered = false)
    {
        var i = 1;
        foreach (var line in lines)
        {
            var prefix = numbered ? $"{i++}. " : "• ";
            col.Item().PaddingLeft(6).Text(prefix + line);
        }
    }

    private static IContainer HeadCell(IContainer c) => c.Background(Tint).Padding(5).DefaultTextStyle(x => x.SemiBold().FontSize(9));
    private static IContainer BodyCell(IContainer c) => c.BorderBottom(0.5f).BorderColor(Line).Padding(5);

    private static void Footer(PageDescriptor page, string academy, string title) =>
        page.Footer().AlignCenter().Text(t =>
        {
            t.DefaultTextStyle(x => x.FontSize(8).FontColor(Soft));
            t.Span($"{academy} · {title} · Page ");
            t.CurrentPageNumber();
            t.Span(" of ");
            t.TotalPages();
        });

    // ---------------- Monthly plan ----------------

    private static readonly (PlanTrack Track, string Title)[] PlanColumns =
    {
        (PlanTrack.Hifz, "HIFZ GOAL"), (PlanTrack.Qiraah, "QIRA’AH GOAL"),
        (PlanTrack.Murajaah, "MURAJAH GOAL"), (PlanTrack.Tajweed, "TAJWEED GOAL")
    };

    private static IContainer InfoCell(IContainer c) => c.BorderBottom(0.6f).BorderColor(Line).PaddingVertical(6);
    private static IContainer GoalBox(IContainer c) => c.Border(0.6f).BorderColor(Line).Padding(8).AlignMiddle().AlignCenter();
    private static IContainer GoalBoxTall(IContainer c) => c.Border(0.6f).BorderColor(Line).Padding(8).MinHeight(60).AlignMiddle().AlignCenter();

    private async Task<byte[]?> LogoBytesAsync(QuranAnchor.Application.Models.BrandingSettings brand, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(brand.LogoPath)) return null;
        var clean = brand.LogoPath.Split('?')[0].TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var path = Path.Combine(env.ContentRootPath, "wwwroot", clean);
        return File.Exists(path) ? await File.ReadAllBytesAsync(path, ct) : null;
    }

    public async Task<byte[]> PlanPdfAsync(Guid planId, CancellationToken ct = default)
    {
        EnsureFonts();
        var plan = await db.Plans.AsNoTracking().Include(p => p.Student).Include(p => p.Tutor).Include(p => p.Goals)
            .FirstAsync(p => p.Id == planId, ct);
        var brand = await settings.GetBrandingAsync(ct);
        var rules = await settings.GetSystemAsync(ct);
        var logo = await LogoBytesAsync(brand, ct);
        var month = MonthName(plan.Year, plan.Month);

        string Goal(int week, PlanTrack track)
        {
            var text = plan.Goals.FirstOrDefault(g => g.Week == week && g.Track == track)?.Text;
            return string.IsNullOrWhiteSpace(text) ? "Not Applicable" : text;
        }

        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(0);
                page.DefaultTextStyle(t => t.FontColor(Ink).FontSize(11));

                page.Header().Column(h =>
                {
                    h.Item().Background("#17262F").PaddingVertical(22).PaddingHorizontal(36).Row(row =>
                    {
                        row.RelativeItem().AlignMiddle().Column(c =>
                        {
                            if (logo is not null) c.Item().Height(46).Image(logo).FitArea();
                            else c.Item().Text(brand.AcademyName).FontSize(18).Bold().FontColor("#FFFFFF");
                        });
                        row.RelativeItem().AlignMiddle().AlignRight().Text("MONTHLY PLAN").FontSize(30).Bold().FontColor("#FFFFFF");
                    });
                    h.Item().Element(TopBand);
                });

                page.Content().PaddingHorizontal(40).PaddingTop(18).Column(col =>
                {
                    col.Spacing(8);
                    col.Item().Element(c => Bismillah(c, 26));

                    col.Item().PaddingTop(6).Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.ConstantColumn(70); c.RelativeColumn(); });
                        t.Cell().Element(InfoCell).Text("Student").SemiBold().FontColor(Violet);
                        t.Cell().Element(InfoCell).Text(": " + plan.Student?.FullName).SemiBold().FontColor(Violet);
                        t.Cell().Element(InfoCell).Text("Month").SemiBold().FontColor(Violet);
                        t.Cell().Element(InfoCell).Text(": " + month).SemiBold().FontColor(Violet);
                    });

                    col.Item().PaddingTop(14).AlignCenter().Text("POST-CLASS EXPECTATIONS").FontSize(17).Bold();
                    col.Item().Text("After each class:").SemiBold();
                    var n = 1;
                    foreach (var line in Lines(plan.PostClassExpectations))
                        col.Item().PaddingLeft(10).Text($"{n++}. {line}");

                    col.Item().PaddingTop(14).Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.ConstantColumn(70); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); });
                        t.Cell().ColumnSpan(5).Element(GoalBox).Text("MONTH GOAL").FontSize(16).Bold();
                        t.Cell().Element(GoalBox).Text("WEEKS").Bold().FontSize(10);
                        foreach (var (_, title) in PlanColumns) t.Cell().Element(GoalBox).Text(title).Bold().FontSize(10);
                        for (var week = 1; week <= 4; week++)
                        {
                            t.Cell().Element(GoalBoxTall).Text($"WEEK {week}").Bold().FontColor(Magenta);
                            foreach (var (track, _) in PlanColumns) t.Cell().Element(GoalBoxTall).Text(Goal(week, track)).SemiBold().FontSize(10);
                        }
                    });

                    var notes = Lines(plan.ParentNotes);
                    if (notes.Length > 0)
                    {
                        col.Item().PaddingTop(12).Text("Notes").Bold().FontSize(12);
                        foreach (var note in notes) col.Item().PaddingLeft(10).Text("• " + note);
                    }

                    col.Item().PaddingTop(16).Text("Monthly progress report:").Bold().FontSize(12);
                    col.Item().Text(rules.PlanClosingNote);
                    col.Item().PaddingTop(8).Text("May Allah bless our efforts, increase us in beneficial knowledge, and grant every student success in their Qur’an journey. Āmīn.").Bold();
                    col.Item().PaddingTop(14).AlignCenter().Element(c =>
                    {
                        if (logo is not null) c.Height(44).Image(logo).FitArea();
                        else c.Text(brand.AcademyName).FontSize(16).Bold().FontColor(Violet);
                    });
                });

                page.Footer().PaddingHorizontal(40).PaddingBottom(14).AlignCenter().Text(t =>
                {
                    t.DefaultTextStyle(x => x.FontSize(8).FontColor(Soft));
                    t.Span($"{brand.AcademyName} · Monthly plan · {plan.Student?.FullName} · {month}");
                });
            });
        }).GeneratePdf();
    }

    // ---------------- Monthly report ----------------

    public async Task<byte[]> ReportPdfAsync(Guid studentId, int year, int month, CancellationToken ct = default)
    {
        EnsureFonts();
        var data = await reports.BuildAsync(studentId, year, month, ct);
        var rules = await settings.GetSystemAsync(ct);
        var brand = await settings.GetBrandingAsync(ct);
        var remarks = await db.Reports.AsNoTracking().FirstOrDefaultAsync(r => r.StudentId == studentId && r.Year == year && r.Month == month, ct);

        string Level(double? v) => v is null ? "" : v >= rules.StrongThresholdPercent ? "Strong" : v >= rules.GoodThresholdPercent ? "Good" : "Developing";

        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(32);
                page.DefaultTextStyle(t => t.FontSize(10).FontColor(Ink));
                page.Header().Element(TopBand);
                Footer(page, brand.AcademyName, "Monthly Student Performance Report");

                page.Content().PaddingTop(14).Column(col =>
                {
                    col.Spacing(6);
                    col.Item().AlignCenter().Text("Monthly Student Performance Report").FontSize(20).Bold();
                    col.Item().AlignCenter().Text(brand.AcademyName).FontColor(Soft);

                    col.Item().PaddingTop(8).Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); });
                        t.Cell().Element(BodyCell).Text($"Student: {data.StudentName}");
                        t.Cell().Element(BodyCell).Text($"Student ID: {data.StudentCode}");
                        t.Cell().Element(BodyCell).Text($"Level: {data.Level}");
                        t.Cell().Element(BodyCell).Text($"Tutor: {data.TutorName}");
                        t.Cell().Element(BodyCell).Text($"Reporting month: {MonthName(year, month)}");
                        var carried = data.CarriedIn > 0 ? $" ({data.PackageTotal - data.CarriedIn} + {data.CarriedIn} carried over)" : "";
                        t.Cell().Element(BodyCell).Text($"Sessions held: {data.SessionsHeld} of {data.PackageTotal}{carried}");
                    });

                    col.Item().PaddingTop(6).Background(Tint).Padding(8).Column(c =>
                    {
                        c.Item().Text("Overall").SemiBold().FontColor(Violet);
                        c.Item().Text(data.Overall is null ? data.Classification : $"{data.Classification} ({data.Overall:0}%)").FontSize(14).Bold();
                    });

                    Heading(col, "At a glance");
                    col.Item().Column(c =>
                    {
                        c.Spacing(3);
                        Bar(c, "Class delivery", data.ClassDeliveryRate);
                        Bar(c, "Student attendance", data.AttendanceRate);
                        Bar(c, "Student punctuality", data.PunctualityRate);
                        Bar(c, "On track with lessons", data.OnTrackRate);
                        Bar(c, "Assignment completion", data.AssignmentRate);
                        Bar(c, "Daily task compliance", data.DailyTaskRate);
                        Bar(c, "Goal achievement", data.GoalRate);
                    });
                    col.Item().Text($"Goals: {data.GoalYes} achieved, {data.GoalPartial} partially, {data.GoalNo} not achieved.").FontColor(Soft);

                    Heading(col, "Session record");
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.ConstantColumn(90); c.ConstantColumn(80); c.RelativeColumn(); c.ConstantColumn(60); });
                        t.Header(h =>
                        {
                            h.Cell().Element(HeadCell).Text("Date");
                            h.Cell().Element(HeadCell).Text("Status");
                            h.Cell().Element(HeadCell).Text("Lesson");
                            h.Cell().Element(HeadCell).Text("Goal");
                        });
                        foreach (var l in data.Lines)
                        {
                            t.Cell().Element(BodyCell).Text(l.LocalStart.ToString("ddd d MMM"));
                            t.Cell().Element(BodyCell).Text(l.Status);
                            t.Cell().Element(BodyCell).Text(l.Lesson);
                            t.Cell().Element(BodyCell).Text(l.Goal);
                        }
                    });

                    if (data.Topics.Count > 0)
                    {
                        Heading(col, "Topics covered");
                        Bullets(col, data.Topics);
                    }

                    Heading(col, "Learning environment");
                    col.Item().Text($"Student camera off in {data.StudentCameraOff} class(es); student network issues in {data.StudentNetworkIssues}; teacher network issues in {data.TeacherNetworkIssues}; environment not conducive in {data.EnvironmentIssues}.");

                    Heading(col, "Class delivery scorecard");
                    col.Item().Column(c =>
                    {
                        c.Spacing(3);
                        Bar(c, $"Lesson delivery {Level(data.LessonDeliveryRate)}", data.LessonDeliveryRate);
                        Bar(c, $"Resource usage {Level(data.ResourceUsageRate)}", data.ResourceUsageRate);
                        Bar(c, $"Student engagement {Level(data.EngagementRate)}", data.EngagementRate);
                        Bar(c, $"Feedback quality {Level(data.FeedbackQualityRate)}", data.FeedbackQualityRate);
                    });

                    if (!string.IsNullOrWhiteSpace(remarks?.FocusNextMonth))
                    {
                        Heading(col, "Focus for next month");
                        col.Item().Text(remarks!.FocusNextMonth);
                    }
                    if (!string.IsNullOrWhiteSpace(remarks?.TutorRemark))
                    {
                        Heading(col, "Tutor's closing remark");
                        col.Item().Text(remarks!.TutorRemark);
                    }
                    if (!string.IsNullOrWhiteSpace(remarks?.ParentNote))
                    {
                        Heading(col, "A note for parents");
                        col.Item().Text(remarks!.ParentNote);
                    }
                });
            });
        }).GeneratePdf();
    }


    // ---------------- Certificate ----------------

    public async Task<byte[]> CertificatePdfAsync(Guid certificateId, CancellationToken ct = default)
    {
        EnsureFonts();
        var cert = await db.Certificates.AsNoTracking().Include(c => c.Student).FirstAsync(c => c.Id == certificateId, ct);
        var brand = await settings.GetBrandingAsync(ct);
        var verifyUrl = (config["App:BaseUrl"] ?? string.Empty).TrimEnd('/') + "/Certificates/Verify/" + cert.Code;

        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(26);
                page.DefaultTextStyle(t => t.FontColor(Ink));

                page.Content().Border(3).BorderColor(Violet).Padding(4).Border(1).BorderColor(Cyan).Column(col =>
                {
                    col.Item().Element(TopBand);
                    col.Item().PaddingTop(18).Element(c => Bismillah(c, 22));
                    col.Item().PaddingTop(6).AlignCenter().Text("CERTIFICATE OF ACHIEVEMENT").FontSize(24).Bold().LetterSpacing(0.08f);
                    col.Item().AlignCenter().Text(brand.AcademyName).FontSize(13).FontColor(Soft);

                    col.Item().PaddingTop(20).AlignCenter().Text("This certificate is proudly presented to").FontSize(13).FontColor(Soft);
                    col.Item().PaddingTop(6).AlignCenter().Text(cert.Student?.FullName ?? "").FontSize(34).Bold().FontColor(Violet);
                    col.Item().PaddingTop(10).AlignCenter().Text("in recognition of").FontSize(13).FontColor(Soft);
                    col.Item().PaddingTop(4).AlignCenter().Text(cert.Title).FontSize(20).SemiBold();
                    if (!string.IsNullOrWhiteSpace(cert.Description))
                        col.Item().PaddingTop(6).PaddingHorizontal(60).AlignCenter().Text(cert.Description).FontSize(12).FontColor(Soft);

                    col.Item().PaddingTop(26).PaddingHorizontal(50).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().AlignCenter().Text(cert.IssuedOn.ToString("d MMMM yyyy")).FontSize(12);
                            c.Item().PaddingTop(3).BorderTop(0.8f).BorderColor(Soft).AlignCenter().Text("Date").FontSize(10).FontColor(Soft);
                        });
                        row.ConstantItem(60);
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().AlignCenter().Text(cert.IssuedByName).FontSize(12);
                            c.Item().PaddingTop(3).BorderTop(0.8f).BorderColor(Soft).AlignCenter().Text("Tutor").FontSize(10).FontColor(Soft);
                        });
                    });

                    col.Item().PaddingTop(18).AlignCenter().Text($"Certificate no. {cert.Code}  ·  Check it at {verifyUrl}").FontSize(8.5f).FontColor(Soft);
                });
            });
        }).GeneratePdf();
    }

    private static void Bar(ColumnDescriptor col, string label, double? value)
    {
        if (value is null) return;
        var pct = Math.Clamp(value.Value, 0, 100);
        col.Item().Row(row =>
        {
            row.ConstantItem(170).Text(label);
            row.RelativeItem().PaddingTop(3).Row(bar =>
            {
                if (pct > 0) bar.RelativeItem((float)pct).Height(8).Background(Violet);
                if (pct < 100) bar.RelativeItem((float)(100 - pct)).Height(8).Background(Line);
            });
            row.ConstantItem(45).AlignRight().Text($"{value:0}%");
        });
    }
}
