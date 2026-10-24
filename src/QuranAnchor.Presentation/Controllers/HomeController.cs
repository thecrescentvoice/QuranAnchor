using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuranAnchor.Application.Abstractions;
using QuranAnchor.Application.Common;
using QuranAnchor.Application.Security;
using QuranAnchor.Domain.Constants;
using QuranAnchor.Domain.Entities;
using QuranAnchor.Domain.Enums;
using QuranAnchor.Infrastructure.Persistence;
using QuranAnchor.Presentation.Services;
using QuranAnchor.Presentation.ViewModels;

namespace QuranAnchor.Presentation.Controllers;

public class HomeController(
    QuranAnchorDbContext db,
    ISchedulingService scheduling,
    ISettingsService settings,
    IReportService reports,
    CurrentUser me) : Controller
{
    [Authorize]
    public async Task<IActionResult> Index()
    {
        var role = me.Role;
        var rules = await settings.GetSystemAsync();
        var now = DateTime.UtcNow;

        var model = new DashboardData
        {
            FullName = User.FindFirst(AppClaimTypes.FullName)?.Value ?? User.Identity?.Name ?? string.Empty,
            Role = role,
            RoleName = Roles.DisplayName(role),
            CanEditBranding = RolePermissions.Has(role, Permissions.EditBranding),
            CanViewAuditLog = RolePermissions.Has(role, Permissions.ViewAuditLog)
        };

        var audience = role == Roles.Student ? new[] { AnnouncementAudience.Everyone, AnnouncementAudience.Students }
            : role == Roles.Tutor ? new[] { AnnouncementAudience.Everyone, AnnouncementAudience.Tutors }
            : new[] { AnnouncementAudience.Everyone, AnnouncementAudience.Students, AnnouncementAudience.Tutors };
        model.Announcements = await db.Announcements.AsNoTracking().Where(a => audience.Contains(a.Audience)).OrderByDescending(a => a.CreatedAtUtc).Take(3).ToListAsync();

        if (me.IsStudent)
        {
            var student = await me.StudentAsync();
            if (student is not null)
            {
                model.Student = student;
                model.Zone = student.TimeZoneId;
                model.Fee = FeeRules.StateOf(student, rules);
                model.Upcoming = await db.Sessions.AsNoTracking().Where(s => s.StudentId == student.Id && s.Status == SessionStatus.Scheduled && s.StartUtc > now.AddMinutes(-60)).OrderBy(s => s.StartUtc).Take(6).ToListAsync();
                model.JoinNow = model.Upcoming.FirstOrDefault(s => now >= s.StartUtc.AddMinutes(-rules.JoinOpensMinutesBefore) && now <= s.EndUtc);
                var today = TimeHelper.LocalToday(student.TimeZoneId);
                var package = await db.Packages.AsNoTracking().FirstOrDefaultAsync(p => p.StudentId == student.Id && p.Year == today.Year && p.Month == today.Month);
                if (package is not null) model.Usage = await scheduling.UsageAsync(package.Id);
            }
        }
        else if (me.IsTutor)
        {
            var tutor = await me.TutorAsync();
            if (tutor is not null)
            {
                model.Zone = tutor.TimeZoneId;
                var day = TimeHelper.LocalToday(tutor.TimeZoneId);
                var from = TimeHelper.ToUtc(day, TimeOnly.MinValue, tutor.TimeZoneId);
                model.Today = await db.Sessions.AsNoTracking().Include(s => s.Student).Where(s => s.TutorId == tutor.Id && s.Status == SessionStatus.Scheduled && s.StartUtc >= from && s.StartUtc < from.AddDays(1)).OrderBy(s => s.StartUtc).ToListAsync();
                var ended = await db.Sessions.AsNoTracking().Include(s => s.Student).Where(s => s.TutorId == tutor.Id && s.Status == SessionStatus.Scheduled && s.StartUtc < now && s.StartUtc > now.AddDays(-14)).OrderBy(s => s.StartUtc).ToListAsync();
                model.NeedFeedback = ended.Where(s => s.EndUtc < now).ToList();
                var summary = (await reports.TutorSummariesAsync(day.Year, day.Month, tutor.Id)).FirstOrDefault();
                model.MonthAmount = summary?.Amount ?? 0;
                model.MonthHeld = summary?.SessionsHeld ?? 0;
                model.RequestsForMe = await db.RescheduleRequests.AsNoTracking().Include(r => r.Session).ThenInclude(s => s!.Student)
                    .Where(r => r.Status == RequestStatus.Pending && r.Session!.TutorId == tutor.Id && !r.RequestedByTutor).ToListAsync();
            }
        }
        else
        {
            var ended = await db.Sessions.AsNoTracking().Include(s => s.Student).Include(s => s.Tutor).Where(s => s.Status == SessionStatus.Scheduled && s.StartUtc < now && s.StartUtc > now.AddDays(-21)).OrderBy(s => s.StartUtc).ToListAsync();
            model.MissingFeedback = ended.Where(s => s.EndUtc < now.AddHours(-rules.TutorFeedbackReminderHours)).Take(20).ToList();
            model.PendingRequests = await db.RescheduleRequests.AsNoTracking().Include(r => r.Session).ThenInclude(s => s!.Student)
                .Where(r => r.Status == RequestStatus.Pending).OrderBy(r => r.CreatedAtUtc).Take(20).ToListAsync();

            var active = await db.Students.AsNoTracking().Where(s => s.Status == AccountStatus.Active).ToListAsync();
            model.OverdueFees = active.Where(s => FeeRules.StateOf(s, rules) is FeeState.Overdue or FeeState.Locked).Take(20).ToList();
            foreach (var s in active)
            {
                var today = TimeHelper.LocalToday(s.TimeZoneId);
                var package = await db.Packages.AsNoTracking().FirstOrDefaultAsync(p => p.StudentId == s.Id && p.Year == today.Year && p.Month == today.Month);
                if (package is null) continue;
                var usage = await scheduling.UsageAsync(package.Id);
                var nearEnd = usage.Total - usage.Used <= 1;
                var lateInMonth = today.Day >= DateTime.DaysInMonth(today.Year, today.Month) - 7 && usage.Unscheduled > 0;
                if (nearEnd || lateInMonth) model.Ending.Add((s, usage));
            }
        }

        return View(model);
    }

    [AllowAnonymous]
    public IActionResult Error() => View();
}
