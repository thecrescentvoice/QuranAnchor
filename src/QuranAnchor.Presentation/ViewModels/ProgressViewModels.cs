using QuranAnchor.Domain.Entities;

namespace QuranAnchor.Presentation.ViewModels;

/// <summary>One month of a child's progress, counted from classes and their feedback.</summary>
public class ProgressMonth
{
    public string Label { get; set; } = string.Empty;

    // Classes
    public int Held { get; set; }
    public int Absent { get; set; }          // the student did not come, without an excuse
    public int Excused { get; set; }         // the student could not come, with an excuse
    public int OtherNotHeld { get; set; }    // tutor unavailable, technical problem, break day, other

    // From the tutor's feedback on held classes
    public int FeedbackCount { get; set; }
    public int GoalYes { get; set; }
    public int GoalPartly { get; set; }
    public int GoalNo { get; set; }
    public int OnTimeCount { get; set; }
    public int TasksGiven { get; set; }
    public int TasksSubmitted { get; set; }

    /// <summary>Classes held out of classes the student was expected at (excused and tutor-side cancellations are left out).</summary>
    public double? Attendance => Held + Absent == 0 ? null : 100.0 * Held / (Held + Absent);

    /// <summary>"Yes" counts as 1 and "Partially" as half.</summary>
    public double? GoalScore => FeedbackCount == 0 ? null : 100.0 * (GoalYes + 0.5 * GoalPartly) / FeedbackCount;

    public double? TaskRate => TasksGiven == 0 ? null : 100.0 * TasksSubmitted / TasksGiven;
    public double? OnTimeRate => FeedbackCount == 0 ? null : 100.0 * OnTimeCount / FeedbackCount;

    public bool HasData => Held + Absent + Excused + OtherNotHeld > 0;
}

public class RecentLesson
{
    public DateTime LocalDate { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Goal { get; set; } = string.Empty;
    public string OnTrack { get; set; } = string.Empty;
}

public class ProgressViewModel
{
    public StudentProfile Student { get; set; } = null!;
    public int Months { get; set; }
    public List<ProgressMonth> Rows { get; set; } = new();
    public ProgressMonth Total { get; set; } = new();
    public List<RecentLesson> Recent { get; set; } = new();
}
