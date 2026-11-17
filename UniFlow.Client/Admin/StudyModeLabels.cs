using UniFlow.Shared.Admin;

namespace UniFlow.Client.Admin;

// How a study mode is shown on screen - the same on the list and in the dialog
public static class StudyModeLabels
{
    public static string Text(StudyModeOption mode) => mode switch
    {
        StudyModeOption.FullTime => "stacjonarne",
        StudyModeOption.PartTime => "niestacjonarne",
        _ => mode.ToString()
    };
}