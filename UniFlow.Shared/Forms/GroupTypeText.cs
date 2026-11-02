namespace UniFlow.Shared.Forms;

public static class GroupTypeText
{
    // "o zmianę grupy laboratoryjnej"
    public static string Adjective(GroupType groupType) => groupType switch
    {
        GroupType.Dean => "dziekańskiej",
        GroupType.Exercises => "ćwiczeniowej",
        GroupType.Laboratory => "laboratoryjnej",
        GroupType.Project => "projektowej",
        _ => throw new ArgumentOutOfRangeException(nameof(groupType), groupType, "Unknown group type.")
    };

    // Table column and drop-down list
    public static string Name(GroupType groupType) => groupType switch
    {
        GroupType.Dean => "grupa dziekańska",
        GroupType.Exercises => "ćwiczenia",
        GroupType.Laboratory => "laboratorium",
        GroupType.Project => "projekt",
        _ => throw new ArgumentOutOfRangeException(nameof(groupType), groupType, "Unknown group type.")
    };
}