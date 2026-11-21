namespace UniFlow.Shared.Forms;

public static class DeanLeaveText
{
    // The choices on the form: "zdrowotny"
    public static string Kind(DeanLeaveKind kind) => kind switch
    {
        DeanLeaveKind.Health => "zdrowotny",
        DeanLeaveKind.Occasional => "okolicznościowy",
        DeanLeaveKind.Parental => "wychowawczy",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown kind of leave.")
    };

    // "o udzielenie urlopu zdrowotnego" - the PDF
    public static string KindAdjective(DeanLeaveKind kind) => kind switch
    {
        DeanLeaveKind.Health => "zdrowotnego",
        DeanLeaveKind.Occasional => "okolicznościowego",
        DeanLeaveKind.Parental => "wychowawczego",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown kind of leave.")
    };

    // The choices on the form: "semestr zimowy"
    public static string Period(DeanLeavePeriod period) => period switch
    {
        DeanLeavePeriod.WinterSemester => "semestr zimowy",
        DeanLeavePeriod.SummerSemester => "semestr letni",
        DeanLeavePeriod.WholeYear => "cały rok akademicki",
        _ => throw new ArgumentOutOfRangeException(nameof(period), period, "Unknown period.")
    };

    // "na semestr zimowy roku akademickiego 2026/2027" - the PDF and the summary
    public static string PeriodWithYear(DeanLeavePeriod period, string academicYear) => period switch
    {
        DeanLeavePeriod.WinterSemester => $"na semestr zimowy roku akademickiego {academicYear}",
        DeanLeavePeriod.SummerSemester => $"na semestr letni roku akademickiego {academicYear}",
        DeanLeavePeriod.WholeYear => $"na rok akademicki {academicYear}",
        _ => throw new ArgumentOutOfRangeException(nameof(period), period, "Unknown period.")
    };

    // What the student attaches - in the PDF; null when nothing is required
    public static string? DocumentSentence(DeanLeaveKind kind) => kind switch
    {
        DeanLeaveKind.Health => "Dokumentację medyczną dołączam do wniosku.",
        DeanLeaveKind.Parental => "Dokument potwierdzający opiekę nad dzieckiem dołączam do wniosku.",
        _ => null
    };
}