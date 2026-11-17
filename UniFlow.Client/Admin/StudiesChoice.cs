using UniFlow.Shared.Admin;

namespace UniFlow.Client.Admin;

// What was chosen in the studies dialog. Resuming uses only the semester.
public sealed record StudiesChoice(int FieldOfStudyId, int Semester, StudyModeOption StudyMode);