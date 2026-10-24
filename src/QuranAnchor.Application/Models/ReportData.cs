namespace QuranAnchor.Application.Models;

public sealed class ReportSessionLine
{
    public DateTime LocalStart { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Lesson { get; set; } = string.Empty;
    public string Goal { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
}

/// <summary>Everything shown on the monthly report, calculated from class records. Null rates mean "not measured".</summary>
public sealed class ReportData
{
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string StudentCode { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string TutorName { get; set; } = string.Empty;
    public int Year { get; set; }
    public int Month { get; set; }

    public int PackageTotal { get; set; }
    public int CarriedIn { get; set; }
    public int SessionsHeld { get; set; }
    public int SessionsScheduled { get; set; }
    public int SessionsNotHeld { get; set; }

    public double? ClassDeliveryRate { get; set; }
    public double? AttendanceRate { get; set; }
    public double? PunctualityRate { get; set; }
    public double? OnTrackRate { get; set; }
    public double? AssignmentRate { get; set; }
    public double? DailyTaskRate { get; set; }
    public double? GoalRate { get; set; }

    public double? LessonDeliveryRate { get; set; }
    public double? ResourceUsageRate { get; set; }
    public double? EngagementRate { get; set; }
    public double? FeedbackQualityRate { get; set; }

    public double? Overall { get; set; }
    public string Classification { get; set; } = "Not enough data";

    public int GoalYes { get; set; }
    public int GoalPartial { get; set; }
    public int GoalNo { get; set; }
    public int StudentCameraOff { get; set; }
    public int StudentNetworkIssues { get; set; }
    public int TeacherNetworkIssues { get; set; }
    public int EnvironmentIssues { get; set; }

    public List<ReportSessionLine> Lines { get; set; } = new();
    public List<string> Topics { get; set; } = new();
}

public sealed class TutorSummary
{
    public Guid TutorId { get; set; }
    public string TutorName { get; set; } = string.Empty;
    public int StudentsTaught { get; set; }
    public int SessionsHeld { get; set; }
    public int SessionsNotHeld { get; set; }
    public double Hours { get; set; }
    public int FeedbackSubmitted { get; set; }
    public int FeedbackMissing { get; set; }
    public decimal Amount { get; set; }
}

public sealed record PackageUsage(int Total, int Used, int ScheduledAhead, int Unscheduled);
