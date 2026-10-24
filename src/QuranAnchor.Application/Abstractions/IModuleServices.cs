using QuranAnchor.Application.Models;
using QuranAnchor.Domain.Entities;
using QuranAnchor.Domain.Enums;

namespace QuranAnchor.Application.Abstractions;

public interface IIdGenerator
{
    Task<string> NextStudentCodeAsync(CancellationToken ct = default);
    Task<string> NextTutorCodeAsync(CancellationToken ct = default);
}

public interface ISchedulingService
{
    Task<SessionPackage> EnsurePackageAsync(StudentProfile student, int year, int month, int? plannedOverride = null, CancellationToken ct = default);
    Task<int> GenerateSessionsAsync(Guid studentId, CancellationToken ct = default);
    Task<PackageUsage> UsageAsync(Guid packageId, CancellationToken ct = default);

    Task<ClassSession?> FindConflictAsync(Guid tutorId, DateTime startUtc, int durationMinutes, Guid? excludeSessionId = null, CancellationToken ct = default);
    Task<(bool Ok, string? Error)> AddScheduleAsync(Guid studentId, DayOfWeek day, TimeOnly time, int duration, DateOnly from, CancellationToken ct = default);
    Task<(bool Ok, string? Error)> ChangeScheduleAsync(Guid scheduleId, DayOfWeek day, TimeOnly time, int duration, DateOnly from, CancellationToken ct = default);
    Task RemoveScheduleAsync(Guid scheduleId, DateOnly from, CancellationToken ct = default);
    Task<(bool Ok, string? Error)> AddExtraSessionAsync(Guid studentId, DateTime startUtc, int duration, CancellationToken ct = default);

    Task AddBreakDayAsync(BreakDay day, CancellationToken ct = default);
    Task RemoveBreakDayAsync(Guid id, CancellationToken ct = default);
    Task SetOnHoldAsync(Guid studentId, DateOnly? until, CancellationToken ct = default);
    Task ResumeAsync(Guid studentId, CancellationToken ct = default);

    Task<(bool Ok, string? Error)> RequestRescheduleAsync(Guid sessionId, Guid userId, bool byTutor, DateTime proposedStartUtc, string reason, CancellationToken ct = default);
    Task<(bool Ok, string? Error)> RespondRescheduleAsync(Guid requestId, Guid userId, bool accept, bool isOverride, CancellationToken ct = default);
}

public interface IFeedbackService
{
    Task<(bool Ok, string? Error)> SubmitAsync(SessionFeedback feedback, Guid userId, CancellationToken ct = default);
    Task<(bool Ok, string? Error)> MarkNotHeldAsync(Guid sessionId, NotHeldReason reason, string note, CancellationToken ct = default);
    Task<decimal> RateForAsync(Guid tutorId, int durationMinutes, CancellationToken ct = default);
    Task<string> BuildWhatsAppTextAsync(Guid sessionId, CancellationToken ct = default);
}

public interface IReportService
{
    Task<ReportData> BuildAsync(Guid studentId, int year, int month, CancellationToken ct = default);
    Task<List<TutorSummary>> TutorSummariesAsync(int year, int month, Guid? onlyTutorId, CancellationToken ct = default);
}

public interface IPdfService
{
    Task<byte[]> PlanPdfAsync(Guid planId, CancellationToken ct = default);
    Task<byte[]> ReportPdfAsync(Guid studentId, int year, int month, CancellationToken ct = default);
}

public interface IPaymentService
{
    Task<(bool Ok, string? Url, string? Error)> StartCheckoutAsync(Guid studentId, string currency, string callbackUrl, CancellationToken ct = default);
    Task<(bool Ok, string Message)> VerifyAsync(string reference, CancellationToken ct = default);
    Task<bool> HandleWebhookAsync(string body, string signature, CancellationToken ct = default);
    Task MarkManualAsync(Guid studentId, string note, CancellationToken ct = default);
}

public interface INotificationService
{
    Task SendToParentAsync(StudentProfile student, string subject, string bodyHtml, CancellationToken ct = default);
    Task SendToTutorAsync(TutorProfile tutor, string subject, string bodyHtml, CancellationToken ct = default);
    Task SendToSupervisorsAsync(string subject, string bodyHtml, CancellationToken ct = default);
    /// <summary>Wraps plain paragraphs in the academy's email look.</summary>
    Task<string> WrapAsync(string heading, params string[] paragraphs);
}

public sealed record CreateStudentRequest(
    string FullName, string ParentName, string ParentEmail, string Phone, string Country, string TimeZoneId,
    StudentLevel Level, Guid? TutorId, int SessionsPerMonth, int SessionsThisMonth, string MeetingLink,
    List<(DayOfWeek Day, TimeOnly Time, int Duration)> Slots);

public sealed record CreatedAccount(bool Ok, string? Error, string? Code, string? TempPassword);

public interface IAccountService
{
    Task<CreatedAccount> CreateStudentAsync(CreateStudentRequest request, CancellationToken ct = default);
    Task<CreatedAccount> CreateTutorAsync(string fullName, string email, string phone, string timeZoneId, CancellationToken ct = default);
    Task<CreatedAccount> CreateStaffAsync(string fullName, string email, string role, string actorRole, CancellationToken ct = default);
    Task<(bool Ok, string? Error)> SetActiveAsync(Guid userId, bool active, string actorRole, CancellationToken ct = default);
    Task<(bool Ok, string? Error)> DeletePermanentlyAsync(Guid userId, CancellationToken ct = default);
    Task<(bool Ok, string? Error)> ReplaceTutorAsync(Guid leavingTutorId, Guid newTutorId, CancellationToken ct = default);
    Task<(bool Ok, string? Error)> RotateMeetLinkAsync(Guid studentId, string newLink, CancellationToken ct = default);
    Task<string> ResetTempPasswordAsync(Guid userId, CancellationToken ct = default);
}
