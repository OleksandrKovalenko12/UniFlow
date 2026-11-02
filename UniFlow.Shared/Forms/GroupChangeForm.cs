namespace UniFlow.Shared.Forms;

public sealed class GroupChangeForm
{
    public GroupChangeScope Scope { get; set; }

    // Scope = General
    public GroupType GroupType { get; set; }
    public string? CurrentGroup { get; set; }
    public string? RequestedGroup { get; set; }

    // Scope = Subjects
    public List<SubjectGroupChange> Subjects { get; set; } = [];

    public string Justification { get; set; } = "";
}