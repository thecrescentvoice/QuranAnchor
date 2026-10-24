using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuranAnchor.Application.Abstractions;
using QuranAnchor.Application.Models;
using QuranAnchor.Domain.Constants;
using QuranAnchor.Presentation.ViewModels;

namespace QuranAnchor.Presentation.Controllers;

/// <summary>Rules and timings (grace period, absence rule, reminders, report thresholds, plan templates). Super Admin only.</summary>
[Authorize(Policy = Permissions.EditSystemSettings)]
public class RulesController(ISettingsService settings, IAuditLogger audit) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var s = await settings.GetSystemAsync();
        return View(new RulesViewModel
        {
            GracePeriodDays = s.GracePeriodDays, UnexcusedAbsenceUsesSession = s.UnexcusedAbsenceUsesSession,
            NoShowWaitMinutes = s.NoShowWaitMinutes, JoinOpensMinutesBefore = s.JoinOpensMinutesBefore,
            ParentReminderMinutes = s.ParentReminderMinutes, TutorFeedbackReminderHours = s.TutorFeedbackReminderHours,
            SupervisorAlertHours = s.SupervisorAlertHours, FeeReminderDaysBefore = s.FeeReminderDaysBefore,
            MinFeedbackWords = s.MinFeedbackWords, StrongThresholdPercent = s.StrongThresholdPercent,
            GoodThresholdPercent = s.GoodThresholdPercent, PlanExpectationsTemplate = s.PlanExpectationsTemplate,
            PlanParentNotesTemplate = s.PlanParentNotesTemplate
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(RulesViewModel model)
    {
        if (model.GoodThresholdPercent > model.StrongThresholdPercent)
            ModelState.AddModelError(nameof(model.GoodThresholdPercent), "Good must not be higher than Strong.");
        if (!ModelState.IsValid) return View(model);

        await settings.SaveSystemAsync(new SystemSettings
        {
            GracePeriodDays = model.GracePeriodDays, UnexcusedAbsenceUsesSession = model.UnexcusedAbsenceUsesSession,
            NoShowWaitMinutes = model.NoShowWaitMinutes, JoinOpensMinutesBefore = model.JoinOpensMinutesBefore,
            ParentReminderMinutes = model.ParentReminderMinutes, TutorFeedbackReminderHours = model.TutorFeedbackReminderHours,
            SupervisorAlertHours = model.SupervisorAlertHours, FeeReminderDaysBefore = model.FeeReminderDaysBefore,
            MinFeedbackWords = model.MinFeedbackWords, StrongThresholdPercent = model.StrongThresholdPercent,
            GoodThresholdPercent = model.GoodThresholdPercent, PlanExpectationsTemplate = model.PlanExpectationsTemplate ?? "",
            PlanParentNotesTemplate = model.PlanParentNotesTemplate ?? ""
        }, User.Identity?.Name);

        await audit.LogAsync("RulesChanged", "System settings");
        TempData["Saved"] = "Rules saved.";
        return RedirectToAction(nameof(Index));
    }
}
