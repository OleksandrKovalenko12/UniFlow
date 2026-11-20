namespace UniFlow.Shared.Forms;

// What the system knows about the student now - the address and the phone are only in USOS.
// The server compares the form with it and puts it into the PDF; the form shows it.
public sealed record CurrentPersonalData(string FirstName, string LastName, string? PrivateEmail);