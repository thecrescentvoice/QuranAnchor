using QuranAnchor.Domain.Enums;

namespace QuranAnchor.Domain.Entities;

/// <summary>The tutor's form after every class.</summary>
public class SessionFeedback
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public ClassSession? Session { get; set; }

    public string LessonTitle { get; set; } = string.Empty;
    public string LearningObjective { get; set; } = string.Empty;
    public GoalResult Goal { get; set; } = GoalResult.Yes;
    public string GoalNote { get; set; } = string.Empty;
    public int TimeUsedMinutes { get; set; }
    public OnTrackStatus OnTrack { get; set; } = OnTrackStatus.OnTrack;
    public string OnTrackComment { get; set; } = string.Empty;
    public OnTrackStatus TaskProgress { get; set; } = OnTrackStatus.OnTrack;
    public string TaskComment { get; set; } = string.Empty;

    public bool StudentOnTime { get; set; } = true;
    public bool TeacherCameraOn { get; set; } = true;
    public bool StudentCameraOn { get; set; } = true;
    public bool TeacherEnvironmentConducive { get; set; } = true;
    public bool StudentEnvironmentConducive { get; set; } = true;
    public bool TeacherNetworkStable { get; set; } = true;
    public bool StudentNetworkStable { get; set; } = true;

    public bool DailyTaskGiven { get; set; } = true;
    public bool DailyTaskSubmitted { get; set; } = true;
    public string TaskText { get; set; } = string.Empty;
    public bool TaskSubmitted { get; set; } = true;

    public string SlidesUrl { get; set; } = string.Empty;
    public string Worksheet { get; set; } = string.Empty;
    public string TeacherNote { get; set; } = string.Empty;

    public Guid SubmittedByUserId { get; set; }
    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;
}

public class MonthlyPlan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StudentId { get; set; }
    public StudentProfile? Student { get; set; }
    public Guid TutorId { get; set; }
    public TutorProfile? Tutor { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    /// <summary>What the student does after every class, one item per line (the plan's "post-class expectations").</summary>
    public string PostClassExpectations { get; set; } = string.Empty;
    /// <summary>An optional extra note for the parent, shown on the plan.</summary>
    public string ParentNotes { get; set; } = string.Empty;
    public PublishStatus Status { get; set; } = PublishStatus.Draft;
    public DateTime? PublishedAtUtc { get; set; }
    public List<PlanGoal> Goals { get; set; } = new();
}

/// <summary>One cell of the month goal grid: a week and a track (Hifz, Qira'ah, Murajaah or Tajweed).</summary>
public class PlanGoal
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlanId { get; set; }
    public MonthlyPlan? Plan { get; set; }
    public int Week { get; set; }
    public PlanTrack Track { get; set; }
    public string Text { get; set; } = "Not Applicable";
}

public class MonthlyReport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StudentId { get; set; }
    public StudentProfile? Student { get; set; }
    public Guid TutorId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public string TutorRemark { get; set; } = string.Empty;
    public string FocusNextMonth { get; set; } = string.Empty;
    public string ParentNote { get; set; } = string.Empty;
    public PublishStatus Status { get; set; } = PublishStatus.Draft;
    public DateTime? PublishedAtUtc { get; set; }
}
