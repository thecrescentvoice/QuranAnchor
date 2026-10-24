namespace QuranAnchor.Application.Models;

/// <summary>Rules and timings the Super Admin can change without a developer.</summary>
public sealed record SystemSettings
{
    public int GracePeriodDays { get; init; } = 7;
    /// <summary>True: a student absent without notice still uses up one session.</summary>
    public bool UnexcusedAbsenceUsesSession { get; init; } = true;
    public int NoShowWaitMinutes { get; init; } = 10;
    public int JoinOpensMinutesBefore { get; init; } = 10;
    public int ParentReminderMinutes { get; init; } = 60;
    public int TutorFeedbackReminderHours { get; init; } = 1;
    public int SupervisorAlertHours { get; init; } = 24;
    public int FeeReminderDaysBefore { get; init; } = 3;
    public int MinFeedbackWords { get; init; } = 15;
    public int StrongThresholdPercent { get; init; } = 90;
    public int GoodThresholdPercent { get; init; } = 70;

    public string PlanExpectationsTemplate { get; init; } =
        "Join each class on time with a quiet place and a stable connection.\nComplete the daily task and send it after every class.\nRevise the previous lesson before the next class.";

    public string PlanParentNotesTemplate { get; init; } =
        "Please make sure your child is ready a few minutes before the class.\nA parent or guardian is welcome to sit in during the first minutes.\nContact the supervisor if you need to change the class days or times.";
}
