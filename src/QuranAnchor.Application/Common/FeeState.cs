using QuranAnchor.Application.Models;
using QuranAnchor.Domain.Entities;

namespace QuranAnchor.Application.Common;

public enum FeeState { Paid, Exempt, Overdue, Locked }

public static class FeeRules
{
    /// <summary>Paid up, overdue but inside the grace period, or locked (plan page only; Join class is never locked).</summary>
    public static FeeState StateOf(StudentProfile student, SystemSettings rules)
    {
        if (student.IsFeeExempt) return FeeState.Exempt;
        var paidUntil = student.PaidUntilUtc ?? DateTime.MinValue;
        var now = DateTime.UtcNow;
        if (paidUntil >= now) return FeeState.Paid;
        return paidUntil.AddDays(rules.GracePeriodDays) >= now ? FeeState.Overdue : FeeState.Locked;
    }
}
