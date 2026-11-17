namespace UniFlow.Client.Admin;

// The audit log's filters survive opening an account and coming back
public sealed class AuditLogState
{
    public string? Search { get; set; }

    // An AuditGroup name, "" = all
    public string Group { get; set; } = "";

    // Days in the browser's (Polish) time, both included
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}