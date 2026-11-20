namespace UniFlow.Shared.Forms;

// What the system knows about the student now - the address and the phone are only in USOS.
// The name goes into the PDF as the old one and must change; the e-mail is only suggested.
public sealed record CurrentPersonalData(string FirstName, string LastName, string? PrivateEmail);