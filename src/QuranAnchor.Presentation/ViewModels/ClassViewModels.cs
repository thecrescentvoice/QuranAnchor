using System.ComponentModel.DataAnnotations;
using QuranAnchor.Domain.Entities;
using QuranAnchor.Domain.Enums;

namespace QuranAnchor.Presentation.ViewModels;

public class SessionDetailsViewModel
{
    public ClassSession Session { get; set; } = null!;
    public StudentProfile Student { get; set; } = null!;
    public TutorProfile Tutor { get; set; } = null!;
    public string ViewerZone { get; set; } = "Africa/Lagos";
    public bool CanJoin { get; set; }
    public bool CanWriteFeedback { get; set; }
    public bool CanMarkNotHeld { get; set; }
    public bool CanRequestReschedule { get; set; }
    public RescheduleRequest? Pending { get; set; }
    public bool CanRespond { get; set; }
    public int PackageTotal { get; set; }
    public int UsedCount { get; set; }
}

public class FeedbackFormViewModel
{
    public Guid SessionId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string WhenText { get; set; } = string.Empty;

    [StringLength(300)][Display(Name = "Today's lesson")] public string LessonTitle { get; set; } = string.Empty;
    [StringLength(500)][Display(Name = "Learning objective")] public string LearningObjective { get; set; } = string.Empty;
    [Display(Name = "Goal achieved")] public GoalResult Goal { get; set; } = GoalResult.Yes;
    [StringLength(500)][Display(Name = "What is left, or why")] public string GoalNote { get; set; } = string.Empty;
    [Range(0, 240)][Display(Name = "Time used (minutes)")] public int TimeUsedMinutes { get; set; }
    [Display(Name = "Student on track with the lesson")] public OnTrackStatus OnTrack { get; set; } = OnTrackStatus.OnTrack;
    [StringLength(500)][Display(Name = "Comment")] public string OnTrackComment { get; set; } = string.Empty;
    [Display(Name = "Student tasks progress")] public OnTrackStatus TaskProgress { get; set; } = OnTrackStatus.OnTrack;
    [StringLength(500)][Display(Name = "Comment")] public string TaskComment { get; set; } = string.Empty;

    [Display(Name = "Student joined on time")] public bool StudentOnTime { get; set; } = true;
    [Display(Name = "Teacher camera on")] public bool TeacherCameraOn { get; set; } = true;
    [Display(Name = "Student camera on")] public bool StudentCameraOn { get; set; } = true;
    [Display(Name = "Teacher environment conducive")] public bool TeacherEnvironmentConducive { get; set; } = true;
    [Display(Name = "Student environment conducive")] public bool StudentEnvironmentConducive { get; set; } = true;
    [Display(Name = "Teacher network stable")] public bool TeacherNetworkStable { get; set; } = true;
    [Display(Name = "Student network stable")] public bool StudentNetworkStable { get; set; } = true;

    [Display(Name = "Daily task given (voice note and written)")] public bool DailyTaskGiven { get; set; } = true;
    [Display(Name = "Daily task submitted (voice note and written)")] public bool DailyTaskSubmitted { get; set; } = true;
    [StringLength(1000)][Display(Name = "Task")] public string TaskText { get; set; } = string.Empty;
    [Display(Name = "Task submitted")] public bool TaskSubmitted { get; set; } = true;

    [StringLength(500)][Display(Name = "Lesson slides link")] public string SlidesUrl { get; set; } = string.Empty;
    [StringLength(500)][Display(Name = "Worksheet")] public string Worksheet { get; set; } = string.Empty;
    [Required(ErrorMessage = "Write a short note.")][StringLength(3000)][Display(Name = "Teacher's note")] public string TeacherNote { get; set; } = string.Empty;

    public SessionFeedback ToEntity() => new()
    {
        SessionId = SessionId, LessonTitle = LessonTitle?.Trim() ?? "", LearningObjective = LearningObjective?.Trim() ?? "",
        Goal = Goal, GoalNote = GoalNote?.Trim() ?? "", TimeUsedMinutes = TimeUsedMinutes,
        OnTrack = OnTrack, OnTrackComment = OnTrackComment?.Trim() ?? "", TaskProgress = TaskProgress, TaskComment = TaskComment?.Trim() ?? "",
        StudentOnTime = StudentOnTime, TeacherCameraOn = TeacherCameraOn, StudentCameraOn = StudentCameraOn,
        TeacherEnvironmentConducive = TeacherEnvironmentConducive, StudentEnvironmentConducive = StudentEnvironmentConducive,
        TeacherNetworkStable = TeacherNetworkStable, StudentNetworkStable = StudentNetworkStable,
        DailyTaskGiven = DailyTaskGiven, DailyTaskSubmitted = DailyTaskSubmitted, TaskText = TaskText?.Trim() ?? "", TaskSubmitted = TaskSubmitted,
        SlidesUrl = SlidesUrl?.Trim() ?? "", Worksheet = Worksheet?.Trim() ?? "", TeacherNote = TeacherNote.Trim()
    };

    public static FeedbackFormViewModel From(SessionFeedback f) => new()
    {
        SessionId = f.SessionId, LessonTitle = f.LessonTitle, LearningObjective = f.LearningObjective, Goal = f.Goal, GoalNote = f.GoalNote,
        TimeUsedMinutes = f.TimeUsedMinutes, OnTrack = f.OnTrack, OnTrackComment = f.OnTrackComment, TaskProgress = f.TaskProgress, TaskComment = f.TaskComment,
        StudentOnTime = f.StudentOnTime, TeacherCameraOn = f.TeacherCameraOn, StudentCameraOn = f.StudentCameraOn,
        TeacherEnvironmentConducive = f.TeacherEnvironmentConducive, StudentEnvironmentConducive = f.StudentEnvironmentConducive,
        TeacherNetworkStable = f.TeacherNetworkStable, StudentNetworkStable = f.StudentNetworkStable,
        DailyTaskGiven = f.DailyTaskGiven, DailyTaskSubmitted = f.DailyTaskSubmitted, TaskText = f.TaskText, TaskSubmitted = f.TaskSubmitted,
        SlidesUrl = f.SlidesUrl, Worksheet = f.Worksheet, TeacherNote = f.TeacherNote
    };
}

public class PlanFormViewModel
{
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public int Year { get; set; }
    public int Month { get; set; }
    public PublishStatus Status { get; set; }

    [StringLength(500)][Display(Name = "Goal for the month")] public string Goal { get; set; } = string.Empty;
    [StringLength(2000)][Display(Name = "Goals for this month (one per line)")] public string MonthGoals { get; set; } = string.Empty;
    [StringLength(2000)][Display(Name = "After every class, the student should (one per line)")] public string PostClassExpectations { get; set; } = string.Empty;
    [StringLength(2000)][Display(Name = "Daily tilawah focus (one per line, for example: Days 1-7: ...)")] public string DailyTilawahFocus { get; set; } = string.Empty;
    [StringLength(500)][Display(Name = "Class format")] public string ClassFormat { get; set; } = string.Empty;
    [StringLength(2000)][Display(Name = "Notes for parents (one per line)")] public string ParentNotes { get; set; } = string.Empty;

    /// <summary>One line per row: sessions | content | activity | muraja'ah   (for example: 1-2 | Waqf signs | Practice | Surah Al-Mulk)</summary>
    [StringLength(6000)][Display(Name = "Weekly plan, one row per line: sessions | content | activity | muraja'ah")]
    public string RowsText { get; set; } = string.Empty;
}

public class ReportRemarksViewModel
{
    public Guid StudentId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    [StringLength(2000)][Display(Name = "Focus for next month")] public string FocusNextMonth { get; set; } = string.Empty;
    [StringLength(2000)][Display(Name = "Closing remark")] public string TutorRemark { get; set; } = string.Empty;
    [StringLength(2000)][Display(Name = "A note for parents")] public string ParentNote { get; set; } = string.Empty;
}

public class PricingViewModel
{
    public List<PortalPrice> Prices { get; set; } = new();
    public List<SessionRate> Rates { get; set; } = new();
    public string NewRateDuration { get; set; } = string.Empty;
    public string NewRateAmount { get; set; } = string.Empty;
}

public class RulesViewModel
{
    [Range(0, 60)][Display(Name = "Grace period after the fee is due (days)")] public int GracePeriodDays { get; set; }
    [Display(Name = "A student absent without notice uses up a session")] public bool UnexcusedAbsenceUsesSession { get; set; }
    [Range(0, 120)][Display(Name = "Wait before marking a student absent (minutes)")] public int NoShowWaitMinutes { get; set; }
    [Range(0, 60)][Display(Name = "Join button opens (minutes before class)")] public int JoinOpensMinutesBefore { get; set; }
    [Range(5, 1440)][Display(Name = "Parent reminder (minutes before class)")] public int ParentReminderMinutes { get; set; }
    [Range(0, 72)][Display(Name = "Remind the tutor about missing feedback after (hours)")] public int TutorFeedbackReminderHours { get; set; }
    [Range(1, 240)][Display(Name = "Alert the supervisor about missing feedback after (hours)")] public int SupervisorAlertHours { get; set; }
    [Range(0, 30)][Display(Name = "Fee reminder (days before it is due)")] public int FeeReminderDaysBefore { get; set; }
    [Range(0, 200)][Display(Name = "Minimum words for a substantive teacher's note")] public int MinFeedbackWords { get; set; }
    [Range(1, 100)][Display(Name = "Strong at or above (%)")] public int StrongThresholdPercent { get; set; }
    [Range(1, 100)][Display(Name = "Good at or above (%)")] public int GoodThresholdPercent { get; set; }
    [StringLength(2000)][Display(Name = "Plan template: after every class, the student should")] public string PlanExpectationsTemplate { get; set; } = string.Empty;
    [StringLength(2000)][Display(Name = "Plan template: notes for parents")] public string PlanParentNotesTemplate { get; set; } = string.Empty;
}

public class AnnouncementFormViewModel
{
    [Required(ErrorMessage = "Enter a title.")][StringLength(200)] public string Title { get; set; } = string.Empty;
    [Required(ErrorMessage = "Write the announcement.")][StringLength(4000)] public string Body { get; set; } = string.Empty;
    public AnnouncementAudience Audience { get; set; } = AnnouncementAudience.Everyone;
    [Display(Name = "Also send by email")] public bool SendEmail { get; set; }
}

public class DashboardData
{
    public string FullName { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Zone { get; set; } = "Africa/Lagos";
    public bool CanEditBranding { get; set; }
    public bool CanViewAuditLog { get; set; }

    // student
    public StudentProfile? Student { get; set; }
    public List<ClassSession> Upcoming { get; set; } = new();
    public ClassSession? JoinNow { get; set; }
    public QuranAnchor.Application.Models.PackageUsage? Usage { get; set; }
    public QuranAnchor.Application.Common.FeeState Fee { get; set; }

    // tutor
    public List<ClassSession> Today { get; set; } = new();
    public List<ClassSession> NeedFeedback { get; set; } = new();
    public decimal MonthAmount { get; set; }
    public int MonthHeld { get; set; }
    public List<RescheduleRequest> RequestsForMe { get; set; } = new();

    // staff
    public List<ClassSession> MissingFeedback { get; set; } = new();
    public List<(StudentProfile Student, QuranAnchor.Application.Models.PackageUsage Usage)> Ending { get; set; } = new();
    public List<RescheduleRequest> PendingRequests { get; set; } = new();
    public List<StudentProfile> OverdueFees { get; set; } = new();

    public List<Announcement> Announcements { get; set; } = new();
}
