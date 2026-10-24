using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuranAnchor.Application.Abstractions;
using QuranAnchor.Application.Models;
using QuranAnchor.Domain.Entities;
using QuranAnchor.Infrastructure.Persistence;

namespace QuranAnchor.Infrastructure.Services;

/// <summary>Makes the downloadable PDFs for the monthly plan and the monthly report.</summary>
public class PdfService(
    QuranAnchorDbContext db,
    IReportService reports,
    ISettingsService settings,
    IHostEnvironment env) : IPdfService
{
    private const string Violet = "#8B5CFF";
    private const string Cyan = "#5CE1E6";
    private const string Magenta = "#D61F9F";
    private const string Ink = "#2F3B3E";
    private const string Soft = "#5B6B6F";
    private const string Line = "#E3E6EF";
    private const string Tint = "#F1F3FA";

    private static bool _fontsReady;
    private static readonly object FontLock = new();

    private void EnsureFonts()
    {
        lock (FontLock)
        {
            if (_fontsReady) return;
            QuestPDF.Settings.License = LicenseType.Community;

            // Put Amiri-Regular.ttf (free from Google Fonts) in wwwroot/fonts so the Arabic bismillah prints correctly.
            var path = Path.Combine(env.ContentRootPath, "wwwroot", "fonts", "Amiri-Regular.ttf");
            if (File.Exists(path))
            {
                using var stream = File.OpenRead(path);
                QuestPDF.Drawing.FontManager.RegisterFontFromStream(stream);
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

    public async Task<byte[]> PlanPdfAsync(Guid planId, CancellationToken ct = default)
    {
        EnsureFonts();
        var plan = await db.Plans.AsNoTracking().Include(p => p.Student).Include(p => p.Tutor).Include(p => p.Rows)
            .FirstAsync(p => p.Id == planId, ct);
        var brand = await settings.GetBrandingAsync(ct);
        var month = MonthName(plan.Year, plan.Month);

        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(32);
                page.DefaultTextStyle(t => t.FontSize(10).FontColor(Ink));
                page.Header().Element(TopBand);
                Footer(page, brand.AcademyName, "Monthly Learning Plan");

                page.Content().PaddingTop(14).Column(col =>
                {
                    col.Spacing(6);
                    col.Item().AlignCenter().Text("بِسْمِ ٱللَّهِ ٱلرَّحْمَٰنِ ٱلرَّحِيمِ").FontFamily("Amiri").FontSize(18);
                    col.Item().AlignCenter().Text("Monthly Learning Plan").FontSize(20).Bold();
                    col.Item().AlignCenter().Text(brand.AcademyName).FontColor(Soft);

                    col.Item().PaddingTop(8).Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); });
                        t.Cell().Element(BodyCell).Text($"Student: {plan.Student?.FullName} ({plan.Student?.StudentCode})");
                        t.Cell().Element(BodyCell).Text($"Month: {month}");
                        t.Cell().Element(BodyCell).Text($"Tutor: {plan.Tutor?.FullName}");
                        t.Cell().Element(BodyCell).Text($"Level: {plan.Student?.Level}");
                    });

                    if (!string.IsNullOrWhiteSpace(plan.Goal))
                    {
                        col.Item().PaddingTop(6).Background(Tint).Padding(8).Column(c =>
                        {
                            c.Item().Text("Goal for the month").SemiBold().FontColor(Violet);
                            c.Item().Text(plan.Goal);
                        });
                    }

                    Heading(col, "After every class, the student should");
                    Bullets(col, Lines(plan.PostClassExpectations));

                    Heading(col, "Goals for this month");
                    Bullets(col, Lines(plan.MonthGoals), numbered: true);

                    Heading(col, "Weekly plan");
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.ConstantColumn(55); c.RelativeColumn(2); c.RelativeColumn(2); c.RelativeColumn(2); });
                        t.Header(h =>
                        {
                            h.Cell().Element(HeadCell).Text("Sessions");
                            h.Cell().Element(HeadCell).Text("Content");
                            h.Cell().Element(HeadCell).Text("Activity");
                            h.Cell().Element(HeadCell).Text("Muraja'ah");
                        });
                        foreach (var row in plan.Rows.OrderBy(r => r.SessionFrom))
                        {
                            var label = row.SessionFrom == row.SessionTo ? $"{row.SessionFrom}" : $"{row.SessionFrom}–{row.SessionTo}";
                            t.Cell().Element(BodyCell).Text(label);
                            t.Cell().Element(BodyCell).Text(row.Content);
                            t.Cell().Element(BodyCell).Text(row.Activity);
                            t.Cell().Element(BodyCell).Text(row.Murajaah);
                        }
                    });

                    var tilawah = Lines(plan.DailyTilawahFocus);
                    if (tilawah.Length > 0)
                    {
                        Heading(col, "Daily tilawah focus");
                        Bullets(col, tilawah);
                    }

                    if (!string.IsNullOrWhiteSpace(plan.ClassFormat))
                    {
                        Heading(col, "Class format");
                        col.Item().Text(plan.ClassFormat);
                    }

                    var notes = Lines(plan.ParentNotes);
                    if (notes.Length > 0)
                    {
                        Heading(col, "Notes for parents");
                        Bullets(col, notes);
                    }

                    Heading(col, "Monthly report");
                    col.Item().Text("At the end of the month you will receive a report with attendance, goal progress, topics covered and the focus for next month.");
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
