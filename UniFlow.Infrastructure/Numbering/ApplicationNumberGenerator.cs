using Microsoft.EntityFrameworkCore;
using UniFlow.Infrastructure.Persistence;
using UniFlow.Infrastructure.Time;

namespace UniFlow.Infrastructure.Numbering;

public sealed class ApplicationNumberGenerator : IApplicationNumberGenerator
{
    private readonly UniFlowDbContext _db;

    public ApplicationNumberGenerator(UniFlowDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<string> NextAsync(DateTime utcNow, CancellationToken cancellationToken = default)
    {
        // Inside a transaction the counter row would stay locked until commit and
        // submissions would queue up - a failed submission leaves a gap instead
        if (_db.Database.CurrentTransaction is not null)
            throw new InvalidOperationException(
                "Take the application number before starting a transaction.");

        var year = PolishTime.FromUtc(utcNow).Year;

        // One atomic statement: first number of the year creates the row, the next ones increment it
        var numbers = await _db.Database.SqlQuery<int>($"""
            INSERT INTO "ApplicationNumberCounters" ("Year", "LastNumber")
            VALUES ({year}, 1)
            ON CONFLICT ("Year") DO UPDATE
                SET "LastNumber" = "ApplicationNumberCounters"."LastNumber" + 1
            RETURNING "LastNumber" AS "Value"
            """).ToListAsync(cancellationToken);

        return ApplicationNumberFormat.Format(year, numbers.Single());
    }
}