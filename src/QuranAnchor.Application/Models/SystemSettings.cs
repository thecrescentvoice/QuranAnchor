namespace QuranAnchor.Application.Models;

/// <summary>Rules and timings the Super Admin can change without a developer.</summary>
public sealed record SystemSettings
{
    public int GracePeriodDays { get; init; } = 7;
    /// <summary>True: a student absent without notice still uses up one session.</summary>
    public bool UnexcusedAbsenceUsesSession { get; init; } = true;
    public int NoShowWaitMinutes { get; init; } = 10;
    public int JoinOpensMinutesBefore { get; init; } = 10;
    public int ParentReminderMinutes { get; init; } = 60;
    public int TutorFeedbackReminderHours { get; init; } = 1;
    public int SupervisorAlertHours { get; init; } = 24;
    public int FeeReminderDaysBefore { get; init; } = 3;
    public int MinFeedbackWords { get; init; } = 15;
    public int StrongThresholdPercent { get; init; } = 90;
    public int GoodThresholdPercent { get; init; } = 70;

    public string PlanExpectationsTemplate { get; init; } =
        "Join each class on time with a quiet place and a stable connection.\nComplete the daily task and send it after every class.\nRevise the previous lesson before the next class.";

    // The dropdown suggestions in the monthly plan grid (one per line). Tutors can also type their own.
    public string PlanHifzOptions { get; init; } =
        "Not Applicable\nRecitation check\nAl-Fātiḥah\nAn-Nās\nAl-Falaq\nAl-Ikhlāṣ\nAl-Masad\nAn-Naṣr\nAl-Kāfirūn\nAl-Kawthar\nAl-Mā‘ūn\nQuraysh\nAl-Fīl\nAl-Humazah\nAl-‘Aṣr\nAt-Takāthur\nAl-Qāri‘ah\nAl-‘Ādiyāt\nAz-Zalzalah\nAl-Bayyinah\nAl-Qadr\nAl-‘Alaq\nAt-Tīn\nAsh-Sharḥ\nAḍ-Ḍuḥā\nAl-Layl\nAsh-Shams";
    public string PlanQiraahOptions { get; init; } =
        "Not Applicable\nLetters and their sounds\nShort vowels (fatḥah, kasrah, ḍammah)\nTanwīn\nSukūn and shaddah\nMadd letters\nConnected forms, Alif to Ḍād\nConnected forms, Ṭā’ to Yā’\nTwo-letter combinations\nSimple Qur’anic words\nShort Qur’anic phrases\nReading from the Muṣḥaf";
    public string PlanMurajaahOptions { get; init; } =
        "Not Applicable\nPrevious lesson\nPrevious connected forms\nAll connected forms\nPrevious surahs\nRecitation check";
    public string PlanTajweedOptions { get; init; } =
        "Not Applicable\nMakhārij al-ḥurūf\nḤarakāt (short vowels)\nMadd rules\nLām Qamariyyah\nLām Shamsiyyah\nIdghām\nIkhfā’\nGhunnah\nQalqalah\nWaqf signs\nRecitation check";
    public string PlanClosingNote { get; init; } =
        "At the end of the month you will receive a progress report covering attendance, class engagement, the goals reached and the tutor’s remarks. It helps us follow progress and see where more support is needed.";

    public string PlanParentNotesTemplate { get; init; } =
        "Please make sure your child is ready a few minutes before the class.\nA parent or guardian is welcome to sit in during the first minutes.\nContact the supervisor if you need to change the class days or times.";
}
