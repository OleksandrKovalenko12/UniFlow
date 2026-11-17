using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using UniFlow.Domain.Entities;
using UniFlow.Domain.Enums;
using UniFlow.Infrastructure.Identity;
using UniFlow.Shared;
using UniFlow.Shared.Admin;
using UniFlow.Shared.ApplicationTypes;

namespace UniFlow.Tests.Infrastructure.Database;

// The administrator's changes to a kind of application
[Collection("Postgres")]
public sealed class AdminApplicationTypesTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private readonly UniFlowApiFactory _factory;
    private HttpClient _client = null!;

    public AdminApplicationTypesTests(PostgresFixture postgres)
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

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string userId,
        string roles, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        request.Headers.Add(TestAuthHandler.UserHeader, userId);
        request.Headers.Add(TestAuthHandler.RolesHeader, roles);
        return await _client.SendAsync(request);
    }

    // A kind of its own - other tests use GROUP_CHANGE, which must stay active
    [Fact]
    public async Task Update_ChangesTheKindLogsItAndHidesItFromStudents()
    {
        var adminId = Guid.NewGuid().ToString("N");
        var type = new ApplicationType($"TEST_{Guid.NewGuid():N}".ToUpperInvariant(), "Wniosek testowy",
            "Opis.", requiresDeanDecision: true, maxAttachments: 2);

        await using (var db = _postgres.CreateContext())
        {
            db.Users.Add(new ApplicationUser
            {
                Id = adminId,
                UserName = adminId,
                Email = $"{adminId}@test.local",
                FirstName = "Adam",
                LastName = "Admin",
                CreatedAt = DateTime.UtcNow,
            });
            db.ApplicationTypes.Add(type);
            await db.SaveChangesAsync();
        }

        var response = await SendAsync(HttpMethod.Put, $"/api/admin/application-types/{type.Id}", adminId,
            RoleNames.Administrator, new ApplicationTypeForm
            {
                Name = "Wniosek testowy",
                Description = "Opis.",
                RequiresDeanDecision = true,
                MaxAttachments = 3,
                IsActive = false
            });

        response.EnsureSuccessStatusCode();
        var saved = (await response.Content.ReadFromJsonAsync<ApplicationTypeDetails>())!;
        Assert.Equal(3, saved.MaxAttachments);
        Assert.False(saved.IsActive);

        await using (var db = _postgres.CreateContext())
        {
            var entry = await db.AuditLogEntries.SingleAsync(e => e.ActorId == adminId);
            Assert.Equal(AuditAction.ApplicationTypeChanged, entry.Action);
            Assert.Equal("Wniosek testowy (limit załączników 2 → 3, wyłączony)", entry.Details);
        }

        // Students no longer see it; the dean's office still filters by it
        var forStudents = await (await SendAsync(HttpMethod.Get, "/api/application-types", adminId,
            RoleNames.Student)).Content.ReadFromJsonAsync<List<ApplicationTypeSummary>>();
        var all = await (await SendAsync(HttpMethod.Get, "/api/application-types?all=true", adminId,
            RoleNames.DeanOffice)).Content.ReadFromJsonAsync<List<ApplicationTypeSummary>>();
        Assert.DoesNotContain(forStudents!, t => t.Id == type.Id);
        Assert.Contains(all!, t => t.Id == type.Id);
    }
}