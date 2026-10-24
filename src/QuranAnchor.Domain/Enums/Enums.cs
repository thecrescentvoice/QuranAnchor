namespace QuranAnchor.Domain.Enums;

public enum StudentLevel { Beginner = 1, Intermediate = 2, Advanced = 3 }
public enum AccountStatus { Active = 1, OnHold = 2, Deactivated = 3 }
public enum SessionStatus { Scheduled = 1, Completed = 2, NotHeld = 3, Rescheduled = 4, Cancelled = 5 }
public enum NotHeldReason { StudentAbsent = 1, StudentExcused = 2, TutorUnavailable = 3, Technical = 4, BreakDay = 5, Other = 6 }
public enum GoalResult { Yes = 1, Partially = 2, No = 3 }
public enum OnTrackStatus { OnTrack = 1, NeedsSupport = 2, Behind = 3 }
public enum RequestStatus { Pending = 1, Accepted = 2, Declined = 3, Cancelled = 4 }
public enum PublishStatus { Draft = 1, Published = 2 }
public enum PaymentStatus { Pending = 1, Succeeded = 2, Failed = 3 }
public enum AnnouncementAudience { Everyone = 1, Tutors = 2, Students = 3 }
