using UniFlow.Shared.Forms;

namespace UniFlow.Documents.Models;

// The current data come from the account, not from the form - the student cannot change them
public sealed record PersonalDataChangeDocument(
    ApplicationHeader Header,
    PersonalDataChangeForm Form,
    CurrentPersonalData Current);