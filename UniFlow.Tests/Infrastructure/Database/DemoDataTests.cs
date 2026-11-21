using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using UniFlow.Api.Demo;
using UniFlow.Domain.Enums;
using UniFlow.Infrastructure.Persistence;
using UniFlow.Infrastructure.Persistence.Seed;

namespace UniFlow.Tests.Infrastructure.Database;

// The demo data: every story fits the rules, every status is there, nothing twice.
// A database of its own - the demo needs an empty one, the other tests fill theirs.
[Collection("Postgres")]
public sealed class DemoDataTests
{
    private const string Password = "Demo!Haslo1";

    private readonly PostgresFixture _postgres;

    public DemoDataTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact]
    public async Task Seed_EmptyDatabase_PlaysEveryStoryOnce()
    {
        var connectionString = new NpgsqlConnectionStringBuilder(_postgres.ConnectionString)
        {
            Database = $"demo_{Guid.NewGuid():N}"
        }.ConnectionString;

        await using var factory = new UniFlowApiFactory(connectionString);
        using (var scope = factory.Services.CreateScope())
        {
            // Creates the database, the roles, the staff and the structure
            await scope.ServiceProvider.GetRequiredService<DevelopmentSeeder>().SeedAsync(Password);
            await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync(Password);
        }

        // A second start finds applications and adds nothing
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync(Password);

        await using var db = new UniFlowDbContext(new DbContextOptionsBuilder<UniFlowDbContext>()
            .UseNpgsql(connectionString).Options);
        try
        {
            var statuses = await db.Applications.Select(a => a.Status).ToListAsync();

            Assert.Equal(DemoStories.Stories.Count, statuses.Count);
            Assert.Equal(Enum.GetValues<ApplicationStatus>().Order(), statuses.Distinct().Order());
            Assert.Equal(DemoStories.Drafts.Count, await db.Drafts.CountAsync());
            // Every kind with a form - the dean's office sees each of them
            Assert.Equal(3, await db.Applications.Select(a => a.ApplicationTypeId).Distinct().CountAsync());
            Assert.True(await db.Applications.SelectMany(a => a.Attachments).AnyAsync(a => a.IsArchived));
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }
}