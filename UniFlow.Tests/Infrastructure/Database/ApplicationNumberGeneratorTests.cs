using UniFlow.Infrastructure.Numbering;

namespace UniFlow.Tests.Infrastructure.Database;

// Every test uses its own year, so the tests do not see each other's counters
[Collection("Postgres")]
public class ApplicationNumberGeneratorTests
{
    private readonly PostgresFixture _postgres;

    public ApplicationNumberGeneratorTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    private static DateTime Utc(int year, int month = 6, int day = 1, int hour = 12, int minute = 0) =>
        new(year, month, day, hour, minute, 0, DateTimeKind.Utc);

    private async Task<string> NextAsync(DateTime utcNow)
    {
        await using var db = _postgres.CreateContext();
        return await new ApplicationNumberGenerator(db).NextAsync(utcNow);
    }

    [Fact]
    public async Task NextAsync_InOneYear_ReturnsConsecutiveNumbers()
    {
        Assert.Equal("UF/2101/0001", await NextAsync(Utc(2101)));
        Assert.Equal("UF/2101/0002", await NextAsync(Utc(2101)));
        Assert.Equal("UF/2101/0003", await NextAsync(Utc(2101)));
    }

    [Fact]
    public async Task NextAsync_NewYear_StartsFromOne()
    {
        await NextAsync(Utc(2102));
        await NextAsync(Utc(2102));

        Assert.Equal("UF/2103/0001", await NextAsync(Utc(2103)));
    }

    [Fact]
    public async Task NextAsync_NewYearEveInUtc_UsesPolishYear()
    {
        // 31.12 23:30 UTC = 1.01 00:30 in Poland
        Assert.Equal("UF/2105/0001", await NextAsync(Utc(2104, 12, 31, 23, 30)));
    }

    [Fact]
    public async Task NextAsync_ManyAtOnce_GivesEveryNumberExactlyOnce()
    {
        var tasks = Enumerable.Range(0, 20).Select(_ => NextAsync(Utc(2106)));

        var numbers = await Task.WhenAll(tasks);

        var expected = Enumerable.Range(1, 20).Select(n => $"UF/2106/{n:D4}");
        Assert.Equal(expected.Order(), numbers.Order());
    }

    // Intended trade-off: a number taken for a submission that failed is not reused
    [Fact]
    public async Task NextAsync_AfterRolledBackSubmission_LeavesGap()
    {
        await using var db = _postgres.CreateContext();
        var generator = new ApplicationNumberGenerator(db);

        var taken = await generator.NextAsync(Utc(2107));
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            // Saving the application failed here
            await transaction.RollbackAsync();
        }

        Assert.Equal("UF/2107/0001", taken);
        Assert.Equal("UF/2107/0002", await generator.NextAsync(Utc(2107)));
    }

    [Fact]
    public async Task NextAsync_InsideTransaction_Throws()
    {
        await using var db = _postgres.CreateContext();
        await using var transaction = await db.Database.BeginTransactionAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new ApplicationNumberGenerator(db).NextAsync(Utc(2108)));
    }
}