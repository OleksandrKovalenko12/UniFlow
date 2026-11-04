namespace UniFlow.Infrastructure.Numbering;

public interface IApplicationNumberGenerator
{
    // Must be called BEFORE the transaction that saves the application
    Task<string> NextAsync(DateTime utcNow, CancellationToken cancellationToken = default);
}