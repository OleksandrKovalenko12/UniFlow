using System.Net.Http.Json;
using UniFlow.Domain.Entities;
using UniFlow.Domain.Enums;
using UniFlow.Infrastructure.Identity;
using UniFlow.Shared;
using UniFlow.Shared.Admin;
using UniFlow.Shared.Review;

namespace UniFlow.Tests.Infrastructure.Database;

// The audit log's filters
[Collection("Postgres")]
public sealed class AdminAuditLogTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private readonly UniFlowApiFactory _factory;
    private HttpClient _client = null!;

    public AdminAuditLogTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        _factory = new UniFlowApiFactory(postgres.ConnectionString);
    }

    public Task InitializeAsync()
    {
        _client = _factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private static ApplicationUser NewUser()
    {
        var id = Guid.NewGuid().ToString("N");
        return new ApplicationUser
        {
            Id = id,
            UserName = id,
            Email = $"{id}@test.local",
            FirstName = "Jan",
            LastName = "Testowy",
            CreatedAt = DateTime.UtcNow,
        };
    }

    [Fact]
    public async Task List_FilteredByAccountGroupAndPolishDays_ReturnsOnlyMatchingEntries()
    {
        var admin = NewUser();
        var account = NewUser();
        var other = NewUser();
        var noon = new DateTime(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc);
        // 23:30 UTC on 10 March is already 11 March in Poland
        var lateEvening = new DateTime(2026, 3, 10, 23, 30, 0, DateTimeKind.Utc);

        await using (var db = _postgres.CreateContext())
        {
            db.Users.AddRange(admin, account, other);
            db.AuditLogEntries.AddRange(
                new AuditLogEntry(admin.Id, AuditAction.RoleAssigned, account.Id, RoleNames.ViceDean, noon),
                new AuditLogEntry(admin.Id, AuditAction.StaffAssigned, account.Id, "WIiT", noon),
                new AuditLogEntry(admin.Id, AuditAction.RoleRemoved, account.Id, RoleNames.Student, lateEvening),
                new AuditLogEntry(admin.Id, AuditAction.RoleAssigned, other.Id, RoleNames.Student, noon));
            await db.SaveChangesAsync();
        }

        var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/admin/audit-log?account={account.Id}&group=Roles&from=2026-03-10&to=2026-03-10");
        request.Headers.Add(TestAuthHandler.UserHeader, admin.Id);
        request.Headers.Add(TestAuthHandler.RolesHeader, RoleNames.Administrator);
        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<PagedList<AuditLogItem>>();
        var entry = Assert.Single(page!.Items);
        Assert.Equal(AuditEntryKind.RoleAssigned, entry.Kind);
        Assert.Equal(RoleNames.ViceDean, entry.Details);
        Assert.Equal("Jan Testowy", entry.ActorName);
    }
}