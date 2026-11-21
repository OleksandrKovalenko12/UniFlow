namespace UniFlow.Shared;

// The academic year a date belongs to, e.g. "2026/2027". Groups are usually known before
// 1 October, so from 20 September on, applications concern the coming year (assumption).
// Shared: the PDF header on the server and the dean's leave form in the browser.
public static class AcademicYear
{
    private const int NewYearMonth = 9;
    private const int NewYearDay = 20;

    // polishTime: a date and time in Poland
    public static int StartYear(DateTime polishTime) =>
        polishTime >= new DateTime(polishTime.Year, NewYearMonth, NewYearDay)
            ? polishTime.Year
            : polishTime.Year - 1;

    public static string Name(int startYear) => $"{startYear}/{startYear + 1}";

    public static string Of(DateTime polishTime) => Name(StartYear(polishTime));
}