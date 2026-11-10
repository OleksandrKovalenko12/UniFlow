using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using UniFlow.Infrastructure.Persistence;

namespace UniFlow.Tests.Infrastructure.Database;

// A fresh PostgreSQL in Docker, shared by all tests in the "Postgres" collection;
// the developer database is never touched
public sealed class PostgresFixture : IAsyncLifetime
{
    // The same image as in docker-compose.yml
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17").Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public string ConnectionString => _container.GetConnectionString();
    // DbContext is not thread-safe - every parallel task needs its own
    public UniFlowDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<UniFlowDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options);
}

[CollectionDefinition("Postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;