namespace UniFlow.Shared.Forms;

public sealed class SubjectGroupChange
{
    public string Subject { get; set; } = "";
    public GroupType GroupType { get; set; }
    public string CurrentGroup { get; set; } = "";
    public string RequestedGroup { get; set; } = "";
}