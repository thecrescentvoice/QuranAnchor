using QuranAnchor.Domain.Entities;

namespace QuranAnchor.Presentation.ViewModels;

public class PerformanceMonth
{
    public string Label { get; set; } = string.Empty;
    public int Held { get; set; }
    public int StudentAbsent { get; set; }
    public int NotHeldOther { get; set; }
    public int FeedbackWritten { get; set; }
    public int FeedbackOnTime { get; set; }
    public int MinutesTaught { get; set; }

    public double? FeedbackRate => Held == 0 ? null : 100.0 * FeedbackWritten / Held;
    public double? OnTimeRate => Held == 0 ? null : 100.0 * FeedbackOnTime / Held;
    public double Hours => Math.Round(MinutesTaught / 60.0, 1);
}

public class PerformanceViewModel
{
    public TutorProfile Tutor { get; set; } = null!;
    public bool IsOwn { get; set; }
    public int Months { get; set; }
    public List<PerformanceMonth> Rows { get; set; } = new();
    public PerformanceMonth Total { get; set; } = new();
    public int StudentsTaught { get; set; }
    public int MissingFeedbackNow { get; set; }
    public double? RatingRecent { get; set; }
    public double? RatingAll { get; set; }
    public int RatingCount { get; set; }
}
