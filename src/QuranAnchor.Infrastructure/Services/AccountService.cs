using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using QuranAnchor.Application.Abstractions;
using QuranAnchor.Application.Common;
using QuranAnchor.Domain.Constants;
using QuranAnchor.Domain.Entities;
using QuranAnchor.Domain.Enums;
using QuranAnchor.Infrastructure.Identity;
using QuranAnchor.Infrastructure.Persistence;

namespace QuranAnchor.Infrastructure.Services;

/// <summary>Creating, deactivating, reactivating and deleting accounts, and handing a tutor's students to a new tutor.</summary>
public class AccountService(
    QuranAnchorDbContext db,
    UserManager<ApplicationUser> users,
    IIdGenerator ids,
    ISchedulingService scheduling,
    INotificationService notify,
    IConfiguration config) : IAccountService
{
    private string LoginUrl => (config["App:BaseUrl"] ?? string.Empty).TrimEnd('/') + "/Account/Login";

    // ---------- Temporary passwords ----------

    private static char Pick(string s) => s[RandomNumberGenerator.GetInt32(s.Length)];

    private static string NewTempPassword()
    {
        const string lower = "abcdefghjkmnpqrstuvwxyz";
        const string upper = "ABCDEFGHJKMNPQRSTUVWXYZ";
        const string digits = "23456789";
        var chars = new List<char> { Pick(lower), Pick(upper), Pick(digits), Pick(digits) };
        while (chars.Count < 10) chars.Add(Pick(lower + upper + digits));
        var array = chars.ToArray();
        RandomNumberGenerator.Shuffle(array.AsSpan());
        return new string(array);
    }

    private static string Errors(IdentityResult r) => string.Join(" ", r.Errors.Select(e => e.Description));

    private async Task<string> WelcomeBodyAsync(string forWhom, string loginName, string temp) =>
        await notify.WrapAsync("Welcome",
            $"Assalamu alaikum. An account has been created for {WebUtility.HtmlEncode(forWhom)}.",
            $"Sign in at <a href=\"{LoginUrl}\">{LoginUrl}</a>",
            $"Your sign-in name: <strong>{WebUtility.HtmlEncode(loginName)}</strong>",
            $"Temporary password: <strong>{WebUtility.HtmlEncode(temp)}</strong>",
            "You will be asked to choose your own password the first time you sign in.");

    // ---------- Create ----------

    public async Task<CreatedAccount> CreateStudentAsync(CreateStudentRequest request, CancellationToken ct = default)
    {
        TutorProfile? tutor = null;
        if (request.TutorId is not null)
        {
            tutor = await db.Tutors.FirstOrDefaultAsync(t => t.Id == request.TutorId, ct);
            if (tutor is null || tutor.Status != AccountStatus.Active) return new(false, "Choose an active tutor.", null, null);
        }

        var code = await ids.NextStudentCodeAsync(ct);
        var temp = NewTempPassword();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(), UserName = code, Email = request.ParentEmail.Trim(), EmailConfirmed = true,
            FullName = request.FullName.Trim(), MustChangePassword = true, IsActive = true
        };

        var created = await users.CreateAsync(user, temp);
        if (!created.Succeeded) return new(false, Errors(created), null, null);
        await users.AddToRoleAsync(user, Roles.Student);

        var student = new StudentProfile
        {
            UserId = user.Id, StudentCode = code, FullName = request.FullName.Trim(), ParentName = request.ParentName.Trim(),
            ParentEmail = request.ParentEmail.Trim(), Phone = request.Phone.Trim(), Country = request.Country.Trim(),
            TimeZoneId = request.TimeZoneId, Level = request.Level, SessionsPerMonth = request.SessionsPerMonth,
            MeetingLink = request.MeetingLink.Trim(), PaidUntilUtc = DateTime.UtcNow, TutorId = tutor?.Id
        };
        db.Students.Add(student);
        if (tutor is not null)
        {
            db.TutorAssignments.Add(new TutorAssignment { StudentId = student.Id, TutorId = tutor.Id, StartedOn = DateOnly.FromDateTime(DateTime.UtcNow) });
        }
        await db.SaveChangesAsync(ct);

        // A student who joins mid-month gets a first package for the rest of that month.
        var today = TimeHelper.LocalToday(student.TimeZoneId);
        await scheduling.EnsurePackageAsync(student, today.Year, today.Month, request.SessionsThisMonth, ct);

        var problems = new List<string>();
        foreach (var slot in request.Slots)
        {
            var result = await scheduling.AddScheduleAsync(student.Id, slot.Day, slot.Time, slot.Duration, today, ct);
            if (!result.Ok && result.Error is not null) problems.Add(result.Error);
        }

        await notify.SendToParentAsync(student, "Your account is ready", await WelcomeBodyAsync(student.FullName, code, temp), ct);
        return new(true, problems.Count > 0 ? string.Join(" ", problems) : null, code, temp);
    }

    public async Task<CreatedAccount> CreateTutorAsync(string fullName, string email, string phone, string timeZoneId, CancellationToken ct = default)
    {
        email = email.Trim();
        if (await users.FindByNameAsync(email) is not null) return new(false, "An account with this email already exists.", null, null);

        var code = await ids.NextTutorCodeAsync(ct);
        var temp = NewTempPassword();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(), UserName = email, Email = email, EmailConfirmed = true,
            FullName = fullName.Trim(), MustChangePassword = true, IsActive = true
        };
        var created = await users.CreateAsync(user, temp);
        if (!created.Succeeded) return new(false, Errors(created), null, null);
        await users.AddToRoleAsync(user, Roles.Tutor);

        db.Tutors.Add(new TutorProfile { UserId = user.Id, TutorCode = code, FullName = fullName.Trim(), Email = email, Phone = phone.Trim(), TimeZoneId = timeZoneId });
        await db.SaveChangesAsync(ct);

        await notify.SendToTutorAsync(new TutorProfile { Email = email }, "Your account is ready", await WelcomeBodyAsync(fullName, email, temp), ct);
        return new(true, null, code, temp);
    }

    public async Task<CreatedAccount> CreateStaffAsync(string fullName, string email, string role, string actorRole, CancellationToken ct = default)
    {
        if (role is not (Roles.Admin or Roles.Supervisor)) return new(false, "Choose Admin or Supervisor.", null, null);
        if (!Roles.CanManage(actorRole, role)) return new(false, "You can only create roles below your own.", null, null);

        email = email.Trim();
        if (await users.FindByNameAsync(email) is not null) return new(false, "An account with this email already exists.", null, null);

        var temp = NewTempPassword();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(), UserName = email, Email = email, EmailConfirmed = true,
            FullName = fullName.Trim(), MustChangePassword = true, IsActive = true
        };
        var created = await users.CreateAsync(user, temp);
        if (!created.Succeeded) return new(false, Errors(created), null, null);
        await users.AddToRoleAsync(user, role);

        await notify.SendToTutorAsync(new TutorProfile { Email = email }, "Your account is ready", await WelcomeBodyAsync(fullName, email, temp), ct);
        return new(true, null, email, temp);
    }

    // ---------- Deactivate / reactivate / delete ----------

    public async Task<(bool Ok, string? Error)> SetActiveAsync(Guid userId, bool active, string actorRole, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null) return (false, "Account not found.");

        var role = (await users.GetRolesAsync(user)).FirstOrDefault() ?? Roles.Student;
        if (!Roles.CanManage(actorRole, role)) return (false, "You cannot manage this role.");

        await ApplyActiveAsync(user, active, ct);
        return (true, null);
    }

    private async Task ApplyActiveAsync(ApplicationUser user, bool active, CancellationToken ct)
    {
        user.IsActive = active;
        user.DeactivatedAtUtc = active ? null : DateTime.UtcNow;
        await users.UpdateAsync(user);

        if (active)
        {
            await users.SetLockoutEndDateAsync(user, null);
            await users.ResetAccessFailedCountAsync(user);
        }
        else
        {
            await users.SetLockoutEnabledAsync(user, true);
            await users.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        }
        await users.UpdateSecurityStampAsync(user);   // signs the person out within a minute

        var student = await db.Students.FirstOrDefaultAsync(s => s.UserId == user.Id, ct);
        if (student is not null)
        {
            student.Status = active ? AccountStatus.Active : AccountStatus.Deactivated;
            student.HoldUntil = null;
            if (!active)
            {
                var future = await db.Sessions.Where(s => s.StudentId == student.Id && s.Status == SessionStatus.Scheduled && s.StartUtc > DateTime.UtcNow).ToListAsync(ct);
                foreach (var s in future) { s.Status = SessionStatus.Cancelled; s.NotHeldReason = NotHeldReason.Other; s.NotHeldNote = "Account deactivated"; }
            }
            await db.SaveChangesAsync(ct);
            if (active) await scheduling.GenerateSessionsAsync(student.Id, ct);
        }

        var tutor = await db.Tutors.FirstOrDefaultAsync(t => t.UserId == user.Id, ct);
        if (tutor is not null)
        {
            tutor.Status = active ? AccountStatus.Active : AccountStatus.Deactivated;
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task<(bool Ok, string? Error)> DeletePermanentlyAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null) return (false, "Account not found.");

        var roles = await users.GetRolesAsync(user);
        if (roles.Contains(Roles.SuperAdmin)) return (false, "A Super Admin account cannot be deleted here.");

        var tutor = await db.Tutors.FirstOrDefaultAsync(t => t.UserId == userId, ct);
        if (tutor is not null)
        {
            if (await db.Sessions.AnyAsync(s => s.TutorId == tutor.Id, ct))
                return (false, "This tutor has class history. Deactivate the account instead, so the records stay accurate.");

            db.Schedules.RemoveRange(db.Schedules.Where(s => s.TutorId == tutor.Id));
            db.TutorAssignments.RemoveRange(db.TutorAssignments.Where(a => a.TutorId == tutor.Id));
            db.Plans.RemoveRange(db.Plans.Where(p => p.TutorId == tutor.Id));
            foreach (var s in await db.Students.Where(s => s.TutorId == tutor.Id).ToListAsync(ct)) s.TutorId = null;
            await db.SaveChangesAsync(ct);
            db.Tutors.Remove(tutor);
            await db.SaveChangesAsync(ct);
        }

        var student = await db.Students.FirstOrDefaultAsync(s => s.UserId == userId, ct);
        if (student is not null)
        {
            db.Students.Remove(student);   // classes, feedback, plans, reports and payments go with it
            await db.SaveChangesAsync(ct);
        }

        var result = await users.DeleteAsync(user);
        return result.Succeeded ? (true, null) : (false, Errors(result));
    }

    // ---------- Replace tutor ----------

    public async Task<(bool Ok, string? Error)> ReplaceTutorAsync(Guid leavingTutorId, Guid newTutorId, CancellationToken ct = default)
    {
        if (leavingTutorId == newTutorId) return (false, "Choose a different tutor.");
        var leaving = await db.Tutors.FirstOrDefaultAsync(t => t.Id == leavingTutorId, ct);
        var incoming = await db.Tutors.FirstOrDefaultAsync(t => t.Id == newTutorId, ct);
        if (leaving is null || incoming is null) return (false, "Tutor not found.");
        if (incoming.Status != AccountStatus.Active) return (false, "The new tutor must be active.");

        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var future = await db.Sessions.Include(s => s.Student).Where(s => s.TutorId == leavingTutorId && s.Status == SessionStatus.Scheduled && s.StartUtc > now).ToListAsync(ct);

        var clashes = new List<string>();
        foreach (var s in future)
        {
            var clash = await scheduling.FindConflictAsync(incoming.Id, s.StartUtc, s.DurationMinutes, null, ct);
            if (clash is not null) clashes.Add($"{s.Student?.FullName} ({TimeHelper.Format(s.StartUtc, incoming.TimeZoneId)})");
        }
        if (clashes.Count > 0) return (false, "The new tutor is already busy at: " + string.Join("; ", clashes.Take(5)) + ". Nothing was changed.");

        var students = await db.Students.Where(s => s.TutorId == leavingTutorId).ToListAsync(ct);
        foreach (var student in students)
        {
            foreach (var open in await db.TutorAssignments.Where(a => a.StudentId == student.Id && a.EndedOn == null).ToListAsync(ct)) open.EndedOn = today;
            db.TutorAssignments.Add(new TutorAssignment { StudentId = student.Id, TutorId = incoming.Id, StartedOn = today });
            student.TutorId = incoming.Id;
        }

        foreach (var slot in await db.Schedules.Where(s => s.TutorId == leavingTutorId && (s.EffectiveTo == null || s.EffectiveTo >= today)).ToListAsync(ct)) slot.TutorId = incoming.Id;
        foreach (var s in future) s.TutorId = incoming.Id;
        await db.SaveChangesAsync(ct);

        // The leaving tutor can no longer sign in. Their past records stay under their own name.
        var leavingUser = await users.FindByIdAsync(leaving.UserId.ToString());
        if (leavingUser is not null) await ApplyActiveAsync(leavingUser, false, ct);
        return (true, null);
    }

    // ---------- Meet link and temporary password ----------

    public async Task<(bool Ok, string? Error)> RotateMeetLinkAsync(Guid studentId, string newLink, CancellationToken ct = default)
    {
        newLink = newLink.Trim();
        if (!Uri.TryCreate(newLink, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            return (false, "Enter a full link that starts with https://");

        var student = await db.Students.FirstOrDefaultAsync(s => s.Id == studentId, ct);
        if (student is null) return (false, "Student not found.");
        student.MeetingLink = newLink;
        student.MeetingLinkChangedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return (true, null);
    }

    public async Task<string> ResetTempPasswordAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(userId.ToString()) ?? throw new InvalidOperationException("Account not found.");
        var temp = NewTempPassword();
        var token = await users.GeneratePasswordResetTokenAsync(user);
        var result = await users.ResetPasswordAsync(user, token, temp);
        if (!result.Succeeded) throw new InvalidOperationException(Errors(result));

        user.MustChangePassword = true;
        await users.UpdateAsync(user);
        await users.SetLockoutEndDateAsync(user, null);
        await users.ResetAccessFailedCountAsync(user);
        return temp;
    }
}
