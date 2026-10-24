using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using QuranAnchor.Domain.Constants;
using QuranAnchor.Domain.Entities;
using QuranAnchor.Infrastructure.Persistence;

namespace QuranAnchor.Presentation.Services;

/// <summary>Who is signed in: their role and, for tutors and students, their profile.</summary>
public sealed class CurrentUser(IHttpContextAccessor accessor, QuranAnchorDbContext db)
{
    private StudentProfile? _student;
    private TutorProfile? _tutor;
    private bool _studentLoaded, _tutorLoaded;

    private ClaimsPrincipal Principal => accessor.HttpContext!.User;

    public Guid UserId => Guid.TryParse(Principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
    public string Role => Roles.All.FirstOrDefault(Principal.IsInRole) ?? Roles.Student;
    public bool IsStudent => Role == Roles.Student;
    public bool IsTutor => Role == Roles.Tutor;
    public bool IsStaff => Role is Roles.SuperAdmin or Roles.Admin or Roles.Supervisor;
    public bool Can(string permission) => QuranAnchor.Application.Security.RolePermissions.Has(Role, permission);

    public async Task<StudentProfile?> StudentAsync()
    {
        if (!_studentLoaded)
        {
            _student = await db.Students.Include(s => s.Tutor).FirstOrDefaultAsync(s => s.UserId == UserId);
            _studentLoaded = true;
        }
        return _student;
    }

    public async Task<TutorProfile?> TutorAsync()
    {
        if (!_tutorLoaded)
        {
            _tutor = await db.Tutors.FirstOrDefaultAsync(t => t.UserId == UserId);
            _tutorLoaded = true;
        }
        return _tutor;
    }

    /// <summary>Staff see every student, a tutor sees their own students, a student sees only themselves.</summary>
    public async Task<bool> CanSeeStudentAsync(StudentProfile student)
    {
        if (IsStaff) return true;
        if (IsStudent) return (await StudentAsync())?.Id == student.Id;
        if (IsTutor)
        {
            var tutor = await TutorAsync();
            if (tutor is null) return false;
            return student.TutorId == tutor.Id || await db.Sessions.AnyAsync(s => s.StudentId == student.Id && s.TutorId == tutor.Id);
        }
        return false;
    }
}
