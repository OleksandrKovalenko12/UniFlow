namespace UniFlow.Api.Applications;

// Once a day deletes drafts nobody has touched for Draft.RetentionDays days, with their files:
// unfinished forms hold personal data and must not stay forever
public sealed class DraftCleanupService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromDays(1);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<DraftCleanupService> _logger;

    public DraftCleanupService(IServiceScopeFactory scopes, ILogger<DraftCleanupService> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        // Once at start-up, then every day
        do
        {
            try
            {
                // DbContext lives per request, so each run gets its own scope
                using var scope = _scopes.CreateScope();
                var drafts = scope.ServiceProvider.GetRequiredService<DraftService>();

                var deleted = await drafts.DeleteExpiredAsync(DateTime.UtcNow, stoppingToken);
                if (deleted > 0)
                    _logger.LogInformation("Deleted {Count} expired drafts.", deleted);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // A failed run waits for the next one - it must not stop the application
                _logger.LogError(exception, "Deleting expired drafts failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}