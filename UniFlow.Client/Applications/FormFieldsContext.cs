using Microsoft.AspNetCore.Components;

namespace UniFlow.Client.Applications;

// What the fields of a kind get from ApplicationForm and ApplicationCorrection: the form, its
// errors and whom to tell about a change (checking and saving the draft)
public sealed record FormFieldsContext<TForm>(TForm Form, FormErrors Errors, EventCallback OnChanged);