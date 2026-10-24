using System.ComponentModel.DataAnnotations;
using QuranAnchor.Domain.Entities;
using QuranAnchor.Domain.Enums;

namespace QuranAnchor.Presentation.ViewModels;

public class SlotInput
{
    public DayOfWeek? Day { get; set; }
    public TimeOnly? Time { get; set; }
    public int? Duration { get; set; }
}

public class StudentFormViewModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Enter the student's name.")][StringLength(200)][Display(Name = "Student's full name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter the parent's name.")][StringLength(200)][Display(Name = "Parent's name")]
    public string ParentName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter the parent's email.")][EmailAddress(ErrorMessage = "Enter a valid email.")][StringLength(256)]
    [Display(Name = "Parent's email (used for login emails and reminders)")]
    public string ParentEmail { get; set; } = string.Empty;

    [StringLength(40)][Display(Name = "Phone")]
    public string Phone { get; set; } = string.Empty;

    [StringLength(80)][Display(Name = "Country")]
    public string Country { get; set; } = string.Empty;

    [Required][Display(Name = "Time zone")]
    public string TimeZoneId { get; set; } = "Africa/Lagos";

    [Display(Name = "Level")]
    public StudentLevel Level { get; set; } = StudentLevel.Beginner;

    [Required(ErrorMessage = "Choose a tutor.")][Display(Name = "Tutor")]
    public Guid? TutorId { get; set; }

    [Range(1, 60, ErrorMessage = "Enter a number from 1 to 60.")][Display(Name = "Sessions per month")]
    public int SessionsPerMonth { get; set; } = 8;

    [Range(0, 60, ErrorMessage = "Enter a number from 0 to 60.")][Display(Name = "Sessions for the rest of this month")]
    public int SessionsThisMonth { get; set; } = 8;

    [Url(ErrorMessage = "Enter a full link starting with https://")][StringLength(500)][Display(Name = "Google Meet link")]
    public string MeetingLink { get; set; } = string.Empty;

    public List<SlotInput> Slots { get; set; } = new() { new(), new(), new(), new() };
}

public class TutorFormViewModel
{
    [Required(ErrorMessage = "Enter the tutor's name.")][StringLength(200)][Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter an email.")][EmailAddress][StringLength(256)][Display(Name = "Email (also the sign-in name)")]
    public string Email { get; set; } = string.Empty;

    [StringLength(40)][Display(Name = "Phone")]
    public string Phone { get; set; } = string.Empty;

    [Required][Display(Name = "Time zone")]
    public string TimeZoneId { get; set; } = "Africa/Lagos";
}

public class StaffFormViewModel
{
    [Required(ErrorMessage = "Enter the name.")][StringLength(200)][Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter an email.")][EmailAddress][StringLength(256)][Display(Name = "Email (also the sign-in name)")]
    public string Email { get; set; } = string.Empty;

    [Required][Display(Name = "Role")]
    public string Role { get; set; } = QuranAnchor.Domain.Constants.Roles.Supervisor;
}

public class StudentListItem
{
    public StudentProfile Student { get; set; } = null!;
    public string TutorName { get; set; } = string.Empty;
}

public class StudentDetailsViewModel
{
    public StudentProfile Student { get; set; } = null!;
    public bool IsLockedUserActive { get; set; }
    public List<ClassSchedule> Slots { get; set; } = new();
    public List<ClassSession> Upcoming { get; set; } = new();
    public List<ClassSession> Recent { get; set; } = new();
    public QuranAnchor.Application.Models.PackageUsage? Usage { get; set; }
    public SessionPackage? Package { get; set; }
    public QuranAnchor.Application.Common.FeeState Fee { get; set; }
    public List<MonthlyPlan> Plans { get; set; } = new();
    public List<MonthlyReport> Reports { get; set; } = new();
    public string? TempPassword { get; set; }
}
