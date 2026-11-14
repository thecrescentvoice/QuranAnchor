using System.ComponentModel.DataAnnotations;
using QuranAnchor.Domain.Entities;
using QuranAnchor.Domain.Enums;

namespace QuranAnchor.Presentation.ViewModels;

public class EnquiryFormViewModel
{
    [Required(ErrorMessage = "Enter the parent's or guardian's name.")][StringLength(200)][Display(Name = "Your name (parent or guardian)")]
    public string ParentName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your email.")][EmailAddress(ErrorMessage = "Enter a valid email.")][StringLength(256)][Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter a phone or WhatsApp number.")][StringLength(40)][Display(Name = "Phone or WhatsApp number")]
    public string Phone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter the student's name.")][StringLength(200)][Display(Name = "Student's name")]
    public string StudentName { get; set; } = string.Empty;

    [Range(3, 99, ErrorMessage = "Enter an age from 3 to 99.")][Display(Name = "Student's age")]
    public int? StudentAge { get; set; }

    [Required(ErrorMessage = "Enter your country.")][StringLength(80)][Display(Name = "Country")]
    public string Country { get; set; } = string.Empty;

    [Required][Display(Name = "Your time zone")]
    public string TimeZoneId { get; set; } = "Africa/Lagos";

    [Display(Name = "Reading level")]
    public StudentLevel Level { get; set; } = StudentLevel.Beginner;

    [StringLength(500)][Display(Name = "Days and times that suit you")]
    public string PreferredTimes { get; set; } = string.Empty;

    [StringLength(2000)][Display(Name = "Anything else we should know?")]
    public string Message { get; set; } = string.Empty;

    [Display(Name = "I agree that the academy may contact me about classes.")]
    public bool Agree { get; set; }

    /// <summary>A hidden field only robots fill in.</summary>
    public string? Website { get; set; }
}

public class RatingFormViewModel
{
    public string TutorName { get; set; } = string.Empty;
    public int Year { get; set; }
    public int Month { get; set; }
    public int Stars { get; set; }
    public string Comment { get; set; } = string.Empty;
    public bool AlreadyRated { get; set; }
    public bool HasTutor { get; set; } = true;
}

public class TutorSlotResult
{
    public TutorProfile Tutor { get; set; } = null!;
    public string Status { get; set; } = string.Empty;   // Free, Busy, Outside
    public string Detail { get; set; } = string.Empty;
}

public class TutorRatingRow
{
    public TutorProfile Tutor { get; set; } = null!;
    public double AverageAll { get; set; }
    public double AverageRecent { get; set; }
    public int Count { get; set; }
}

public class MeetingFormViewModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Enter a title.")][StringLength(200)][Display(Name = "Title")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Choose the date and time.")][Display(Name = "Date and time")]
    public DateTime? When { get; set; }

    [Required][Display(Name = "Time zone you are entering the time in")]
    public string TimeZoneId { get; set; } = "Africa/Lagos";

    [Range(15, 480, ErrorMessage = "Enter from 15 to 480 minutes.")][Display(Name = "Length (minutes)")]
    public int DurationMinutes { get; set; } = 60;

    [Url(ErrorMessage = "Enter a full link starting with https://")][StringLength(500)][Display(Name = "Meeting link (Google Meet, Zoom...)")]
    public string MeetingLink { get; set; } = string.Empty;

    [StringLength(4000)][Display(Name = "Agenda")]
    public string Agenda { get; set; } = string.Empty;

    [Display(Name = "Invite every tutor")]
    public bool AllTutors { get; set; } = true;

    public List<Guid> SelectedTutorIds { get; set; } = new();

    [StringLength(4000)][Display(Name = "Notes or minutes (after the meeting)")]
    public string Notes { get; set; } = string.Empty;
}
