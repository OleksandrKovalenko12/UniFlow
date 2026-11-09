namespace UniFlow.Shared.ApplicationTypes;

public sealed record ApplicationTypeSummary(
    int Id,
    string Code,
    string Name,
    string Description);