using UniFlow.Domain.Enums;
using UniFlow.Shared.Forms;

namespace UniFlow.Api.Demo;

// The demo data as a story book - who studies what and what happened to their applications.
// Only data: DemoDataSeeder plays it through the same steps a real user goes through.
// Times are days before the day the data is created, at a Polish hour, so the lists always
// look recent. All people are made up.
public static class DemoStories
{
    // Staff from DevelopmentSeeder
    private const string Anna = "anna.kowalska@uniflow.test";          // dean's office, WIiT
    private const string Katarzyna = "katarzyna.mazur@uniflow.test";   // dean's office, WIiT
    private const string Joanna = "joanna.szymanska@uniflow.test";     // dean's office, WIiT and WPiA
    private const string Ewa = "ewa.dabrowska@uniflow.test";           // dean's office, WPiA
    private const string Piotr = "piotr.wisniewski@uniflow.test";      // vice-dean, WIiT
    private const string Marek = "marek.kaminski@uniflow.test";        // vice-dean, WPiA

    // Students from DevelopmentSeeder
    private const string Jan = "s100001@student.uniflow.test";
    private const string Maria = "s100002@student.uniflow.test";
    private const string Tomasz = "s100003@student.uniflow.test";

    private const string Informatics = "Informatyka";
    private const string Teleinformatics = "Teleinformatyka";
    private const string Law = "Prawo";
    private const string Administration = "Administracja";

    // New students. Ended studies: the student no longer studies there.
    public static readonly IReadOnlyList<DemoStudent> Students =
    [
        new("s100005@student.uniflow.test", "Zofia", "Kaczmarek", "100005",
            [new(Informatics, StudyLevel.FirstCycle, 1, StudyMode.FullTime)]),
        new("s100006@student.uniflow.test", "Michał", "Piotrowski", "100006",
            [new(Informatics, StudyLevel.FirstCycle, 5, StudyMode.FullTime)]),
        new("s100007@student.uniflow.test", "Natalia", "Grabowska", "100007",
            [new(Teleinformatics, StudyLevel.FirstCycle, 3, StudyMode.FullTime)]),
        new("s100008@student.uniflow.test", "Jakub", "Pawlak", "100008",
            [new(Informatics, StudyLevel.SecondCycle, 3, StudyMode.PartTime)]),
        new("s100009@student.uniflow.test", "Wiktoria", "Michalska", "100009",
            [new(Law, StudyLevel.LongCycle, 7, StudyMode.FullTime)]),
        new("s100010@student.uniflow.test", "Kacper", "Król", "100010",
            [new(Law, StudyLevel.LongCycle, 3, StudyMode.FullTime)]),
        new("s100011@student.uniflow.test", "Julia", "Wieczorek-Lis", "100011",
            [new(Administration, StudyLevel.FirstCycle, 2, StudyMode.FullTime)]),
        // Two studies on two faculties - each goes to its own dean's office
        new("s100012@student.uniflow.test", "Szymon", "Jabłoński", "100012",
            [new(Administration, StudyLevel.FirstCycle, 6, StudyMode.PartTime),
             new(Informatics, StudyLevel.FirstCycle, 2, StudyMode.PartTime)]),
        new("s100013@student.uniflow.test", "Amelia", "Majewska", "100013",
            [new(Informatics, StudyLevel.FirstCycle, 7, StudyMode.FullTime)]),
        // Graduated - the studies end and the account is switched off after the last application
        new("s100014@student.uniflow.test", "Filip", "Olszewski", "100014",
            [new(Informatics, StudyLevel.FirstCycle, 8, StudyMode.FullTime, Ended: true)]),
    ];

    // Created by the administrator a day ago - has not set their own password yet
    public static readonly DemoStudent NewcomerWithoutPassword =
        new("s100015@student.uniflow.test", "Maja", "Zawadzka", "100015",
            [new(Administration, StudyLevel.FirstCycle, 1, StudyMode.FullTime)]);

    public static readonly IReadOnlyList<DemoStory> Stories =
    [
        // ---------- WIiT: closed long ago
        new("s100014@student.uniflow.test", Informatics, StudyLevel.FirstCycle,
            General(GroupType.Dean, "2", "1", "Zmiana pracy - od marca pracuję na popołudniowej zmianie."),
            [Submitted(At(28, 9, 12)), Taken(Anna, At(27, 10, 5)),
             Forwarded(Anna, At(26, 11, 40), "Wolne miejsca w grupie 1, opinia pozytywna."),
             Approved(Piotr, At(25, 13, 15))]),

        new("s100013@student.uniflow.test", Informatics, StudyLevel.FirstCycle,
            General(GroupType.Laboratory, "4", "2", "Kolizja z zajęciami sportowymi w AZS."),
            [Submitted(At(27, 18, 30)), Withdrawn(At(26, 8, 50))]),

        new("s100006@student.uniflow.test", Informatics, StudyLevel.FirstCycle,
            General(GroupType.Exercises, "3", "6", "Chciałbym być w jednej grupie ze znajomymi z projektu."),
            [Submitted(At(25, 20, 10)), Taken(Anna, At(24, 9, 30)),
             Rejected(Anna, At(23, 10, 0), "Grupa 6 jest pełna. Proszę złożyć wniosek o inną grupę ćwiczeniową.")]),

        new("s100007@student.uniflow.test", Teleinformatics, StudyLevel.FirstCycle,
            General(GroupType.Project, "1", "2", "Proszę o zmianę."),
            [Submitted(At(24, 7, 45)), Taken(Joanna, At(23, 12, 20)),
             Rejected(Joanna, At(22, 9, 5), "Brak uzasadnienia - wniosek nie wskazuje powodu zmiany.")]),

        new("s100006@student.uniflow.test", Informatics, StudyLevel.FirstCycle,
            Subjects("Kolizja dwóch laboratoriów w piątek o 10:00.",
                ("Bazy danych", GroupType.Laboratory, "2", "4"),
                ("Sieci komputerowe", GroupType.Laboratory, "5", "3")),
            [Submitted(At(22, 21, 0)), Taken(Anna, At(21, 8, 40)),
             Forwarded(Anna, At(20, 9, 15), "Kolizja potwierdzona w planie zajęć."),
             Approved(Piotr, At(19, 14, 30))]),

        // ---------- WIiT: closed recently
        new(Jan, Informatics, StudyLevel.FirstCycle,
            General(GroupType.Laboratory, "3", "5", "Praca na pół etatu w godzinach porannych - załączam zaświadczenie od pracodawcy."),
            [Submitted(At(20, 16, 20), files: 1), Taken(Anna, At(19, 9, 0)),
             Forwarded(Anna, At(18, 10, 30), "Zgodne z regulaminem, w grupie 5 są wolne miejsca."),
             Approved(Piotr, At(16, 12, 0))]),

        new("s100008@student.uniflow.test", Informatics, StudyLevel.SecondCycle,
            General(GroupType.Dean, "1", "2", "Studia niestacjonarne łączę z pracą zmianową - grafik w załącznikach."),
            [Submitted(At(15, 19, 40), files: 2), Taken(Katarzyna, At(14, 8, 15)),
             Forwarded(Katarzyna, At(13, 9, 50)),
             Returned(Piotr, At(11, 15, 10), "Proszę sprawdzić, czy w grupie 2 są wolne miejsca."),
             Forwarded(Katarzyna, At(10, 10, 25), "Sprawdzone - w grupie 2 są dwa wolne miejsca."),
             Approved(Piotr, At(9, 11, 0))]),

        new(Maria, Informatics, StudyLevel.SecondCycle,
            General(GroupType.Exercises, "2", "1", "Wolę zajęcia w poniedziałki."),
            [Submitted(At(12, 13, 5)), Taken(Joanna, At(11, 9, 20)),
             Forwarded(Joanna, At(10, 12, 0)),
             Rejected(Piotr, At(8, 10, 45), "Zmiana grupy w trakcie semestru jest możliwa tylko z ważnych powodów - preferencja dnia zajęć nim nie jest.")]),

        new(Maria, Informatics, StudyLevel.SecondCycle,
            General(GroupType.Exercises, "2", "3", "Kolizja z lektoratem."),
            [Submitted(At(7, 17, 30)), Withdrawn(At(6, 8, 10))]),

        // ---------- WIiT: in progress
        new(Tomasz, Teleinformatics, StudyLevel.FirstCycle,
            General(GroupType.Laboratory, "2", "1", "Kolizja z pracą w dziekanacie."),
            [Submitted(At(13, 15, 0)), Taken(Anna, At(12, 9, 35)),
             SentBack(Anna, At(11, 10, 15), "Proszę wskazać konkretne przedmioty, których dotyczy kolizja.")]),

        new("s100007@student.uniflow.test", Teleinformatics, StudyLevel.FirstCycle,
            General(GroupType.Laboratory, "3", "1", "Kolizja z pracą."),
            [Submitted(At(10, 20, 15), files: 1), Taken(Anna, At(9, 8, 30)),
             SentBack(Anna, At(8, 9, 0), "Uzasadnienie jest zbyt ogólne - proszę opisać, z czym kolidują zajęcia."),
             Corrected(At(1, 19, 20), "Pracuję w poniedziałki i środy do 14:00 (zaświadczenie w załączniku), a laboratoria grupy 3 są w te dni o 10:00.")]),

        new("s100013@student.uniflow.test", Informatics, StudyLevel.FirstCycle,
            General(GroupType.Dean, "3", "1", "Praktyki zawodowe w firmie w godzinach popołudniowych - umowa w załączniku."),
            [Submitted(At(9, 12, 40), files: 1), Taken(Katarzyna, At(8, 10, 10)),
             Forwarded(Katarzyna, At(7, 11, 30), "Dokumenty kompletne, w grupie 1 jest miejsce.")]),

        new(Jan, Informatics, StudyLevel.FirstCycle,
            General(GroupType.Project, "2", "4", "Zmiana godzin pracy od przyszłego miesiąca."),
            [Submitted(At(6, 21, 10)), Taken(Katarzyna, At(5, 9, 45)),
             SentBack(Katarzyna, At(1, 10, 30), "Proszę dołączyć zaświadczenie od pracodawcy potwierdzające nowe godziny pracy.")]),

        new("s100012@student.uniflow.test", Informatics, StudyLevel.FirstCycle,
            General(GroupType.Laboratory, "1", "3", "Kolizja z zajęciami na drugim kierunku (Administracja)."),
            [Submitted(At(5, 18, 0)), Taken(Joanna, At(4, 9, 10)),
             Forwarded(Joanna, At(1, 11, 45), "Kolizja potwierdzona w planach obu kierunków.")]),

        new("s100006@student.uniflow.test", Informatics, StudyLevel.FirstCycle,
            General(GroupType.Exercises, "6", "2", "Opieka nad młodszym rodzeństwem w czwartki po południu."),
            [Submitted(At(4, 16, 50)), Taken(Anna, At(3, 10, 20))]),

        new("s100005@student.uniflow.test", Informatics, StudyLevel.FirstCycle,
            General(GroupType.Dean, "4", "2", "Dojeżdżam z innej miejscowości - poranne zajęcia grupy 2 pasują do rozkładu pociągów."),
            [Submitted(At(3, 22, 5))]),

        new("s100008@student.uniflow.test", Informatics, StudyLevel.SecondCycle,
            Subjects("Nowy grafik pracy od przyszłego tygodnia.",
                ("Uczenie maszynowe", GroupType.Laboratory, "1", "2")),
            [Submitted(At(1, 8, 25))]),

        // ---------- WPiA
        new("s100010@student.uniflow.test", Law, StudyLevel.LongCycle,
            General(GroupType.Exercises, "5", "2", "Kolizja z praktyką w kancelarii."),
            [Submitted(At(21, 14, 10)), Taken(Ewa, At(20, 9, 0)),
             Forwarded(Ewa, At(19, 10, 40)),
             Rejected(Marek, At(17, 12, 30), "Praktyka w kancelarii nie jest obowiązkowa - nie uzasadnia zmiany grupy.")]),

        new("s100009@student.uniflow.test", Law, StudyLevel.LongCycle,
            General(GroupType.Exercises, "3", "1", "Udział w konkursie sądowym - treningi zespołu w godzinach zajęć grupy 3."),
            [Submitted(At(18, 11, 30), files: 1), Taken(Ewa, At(17, 8, 50)),
             Forwarded(Ewa, At(16, 9, 30), "Zaświadczenie opiekuna koła naukowego w załączniku."),
             Approved(Marek, At(14, 13, 0))]),

        new("s100012@student.uniflow.test", Administration, StudyLevel.FirstCycle,
            General(GroupType.Dean, "2", "1", "Kolizja z zajęciami na Informatyce - zaświadczenia z obu dziekanatów."),
            [Submitted(At(16, 17, 45), files: 2), Taken(Joanna, At(15, 9, 25)),
             SentBack(Joanna, At(14, 10, 0), "Jedno z zaświadczeń jest nieczytelne - proszę dołączyć je ponownie."),
             Corrected(At(12, 20, 30), files: 1, replaceFirstFile: true),
             Taken(Joanna, At(11, 8, 40)),
             Forwarded(Joanna, At(11, 9, 15)),
             Approved(Marek, At(10, 11, 20))]),

        new("s100011@student.uniflow.test", Administration, StudyLevel.FirstCycle,
            General(GroupType.Exercises, "1", "3", "Proszę o zmianę grupy."),
            [Submitted(At(11, 15, 35)), Taken(Ewa, At(10, 9, 5)),
             Rejected(Ewa, At(9, 10, 50), "Wniosek złożony po terminie zmian grup w tym semestrze.")]),

        new("s100010@student.uniflow.test", Law, StudyLevel.LongCycle,
            General(GroupType.Exercises, "5", "4", "Kolizja z obowiązkowym lektoratem."),
            [Submitted(At(8, 12, 15)), Taken(Ewa, At(7, 9, 40)),
             SentBack(Ewa, At(6, 10, 5), "Proszę dołączyć plan lektoratu z sekretariatu Studium Języków Obcych."),
             Corrected(At(3, 18, 45), files: 1)]),

        new("s100011@student.uniflow.test", Administration, StudyLevel.FirstCycle,
            General(GroupType.Exercises, "1", "2", "Kolizja z obowiązkowymi zajęciami WF."),
            [Submitted(At(5, 14, 0)), Taken(Ewa, At(3, 9, 30))]),

        new(Maria, Law, StudyLevel.LongCycle,
            General(GroupType.Exercises, "4", "2", "Studia na dwóch kierunkach - kolizja z Informatyką w środy."),
            [Submitted(At(4, 19, 15))]),

        new("s100009@student.uniflow.test", Law, StudyLevel.LongCycle,
            General(GroupType.Exercises, "1", "4", "Zmiana składu zespołu konkursowego."),
            [Submitted(At(1, 9, 50))]),
    ];

    // Begun and not submitted - "Niedokończone" on the student's list
    public static readonly IReadOnlyList<DemoDraft> Drafts =
    [
        new(Maria, Informatics, StudyLevel.SecondCycle,
            General(GroupType.Laboratory, "1", "", "Kolizja z"), At(1, 21, 30), Files: 0),
        new("s100012@student.uniflow.test", Administration, StudyLevel.FirstCycle,
            General(GroupType.Exercises, "2", "3", "Nowa praca od przyszłego miesiąca."), At(2, 20, 0), Files: 1),
    ];

    private static DemoTime At(int daysAgo, int hour, int minute) => new(daysAgo, hour, minute);

    private static GroupChangeForm General(GroupType type, string current, string requested,
        string justification) => new()
        {
            Scope = GroupChangeScope.General,
            GroupType = type,
            CurrentGroup = current,
            RequestedGroup = requested,
            Justification = justification
        };

    private static GroupChangeForm Subjects(string justification,
        params (string Subject, GroupType Type, string Current, string Requested)[] subjects) => new()
        {
            Scope = GroupChangeScope.Subjects,
            Subjects = subjects
            .Select(s => new SubjectGroupChange
            {
                Subject = s.Subject,
                GroupType = s.Type,
                CurrentGroup = s.Current,
                RequestedGroup = s.Requested
            })
            .ToList(),
            Justification = justification
        };

    private static DemoStep Submitted(DemoTime at, int files = 0) =>
        new(DemoAction.Submit, at, Files: files);

    private static DemoStep Taken(string by, DemoTime at) => new(DemoAction.Take, at, by);

    private static DemoStep SentBack(string by, DemoTime at, string comment) =>
        new(DemoAction.RequestCorrection, at, by, comment);

    // justification: the corrected one, when the student rewrote it
    private static DemoStep Corrected(DemoTime at, string? justification = null, int files = 0,
        bool replaceFirstFile = false) =>
        new(DemoAction.Resubmit, at, Text: justification, Files: files, ReplaceFirstFile: replaceFirstFile);

    private static DemoStep Forwarded(string by, DemoTime at, string? note = null) =>
        new(DemoAction.Forward, at, by, note);

    private static DemoStep Approved(string by, DemoTime at) => new(DemoAction.Approve, at, by);

    private static DemoStep Rejected(string by, DemoTime at, string comment) =>
        new(DemoAction.Reject, at, by, comment);

    private static DemoStep Returned(string by, DemoTime at, string comment) =>
        new(DemoAction.ReturnToDeanOffice, at, by, comment);

    private static DemoStep Withdrawn(DemoTime at) => new(DemoAction.Withdraw, at);
}

// A moment: so many days before the day the data is created, at a Polish hour
public sealed record DemoTime(int DaysAgo, int Hour, int Minute);

public sealed record DemoStudies(string FieldOfStudy, StudyLevel Level, int Semester, StudyMode Mode,
    bool Ended = false);

public sealed record DemoStudent(string Email, string FirstName, string LastName, string AlbumNumber,
    IReadOnlyList<DemoStudies> Studies);

public enum DemoAction
{
    Submit,
    Take,
    RequestCorrection,
    Resubmit,
    Forward,
    Approve,
    Reject,
    ReturnToDeanOffice,
    Withdraw
}

// One step of a story. By: the employee who acts (the student acts on their own application).
// Text: the comment, the internal note or the corrected justification. Files: new attachments;
// ReplaceFirstFile: in a correction the student removes their first file.
public sealed record DemoStep(DemoAction Action, DemoTime At, string? By = null, string? Text = null,
    int Files = 0, bool ReplaceFirstFile = false);

// One application of a group change, from submission to where it is today
public sealed record DemoStory(string Student, string FieldOfStudy, StudyLevel Level,
    GroupChangeForm Form, IReadOnlyList<DemoStep> Steps);

public sealed record DemoDraft(string Student, string FieldOfStudy, StudyLevel Level,
    GroupChangeForm Form, DemoTime At, int Files);