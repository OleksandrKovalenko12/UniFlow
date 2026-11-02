using UniFlow.Shared.Forms;

namespace UniFlow.Documents.Models;

public sealed record GroupChangeDocument(ApplicationHeader Header, GroupChangeForm Form);