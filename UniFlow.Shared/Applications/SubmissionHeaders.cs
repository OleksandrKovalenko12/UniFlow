namespace UniFlow.Shared.Applications;

public static class SubmissionHeaders
{
    // Client: one random key per form, the same one for every retry of that form
    public const string IdempotencyKey = "Idempotency-Key";
}