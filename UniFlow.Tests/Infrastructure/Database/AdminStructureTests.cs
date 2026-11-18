using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniFlow.Domain.Enums;
using UniFlow.Infrastructure.Identity;
using UniFlow.Shared;
using UniFlow.Shared.Admin;

namespace UniFlow.Tests.Infrastructure.Database;

// Faculties and fields of study, managed by the administrator
[Collection("Postgres")]
public sealed class AdminStructureTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private readonly UniFlowApiFactory _factory;
    private HttpClient _client = null!;
    private string _adminId = null!;

    public AdminStructureTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        _factory = new UniFlowApiFactory(postgres.ConnectionString);
    }

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

        // The acting administrator must exist - the audit log points to them
        _adminId = Guid.NewGuid().ToString("N");
        await using var db = _postgres.CreateContext();
        db.Users.Add(new ApplicationUser
        {
            Id = _adminId,
            UserName = _adminId,
            Email = $"{_adminId}@test.local",
            FirstName = "Adam",
            LastName = "Admin",
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, object body)
    {
        var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) };
        request.Headers.Add(TestAuthHandler.UserHeader, _adminId);
        request.Headers.Add(TestAuthHandler.RolesHeader, RoleNames.Administrator);
        return _client.SendAsync(request);
    }

    // Codes unique per test run - faculties are shared by all database tests
    private static string NewCode() => "T" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private async Task<FacultyItem> AddFacultyAsync(string code)
    {
        var response = await SendAsync(HttpMethod.Post, "/api/admin/faculties",
            new FacultyForm { Code = code, Name = $"Wydział {code}" });
        response.EnsureSuccessStatusCode();
        var structure = await response.Content.ReadFromJsonAsync<List<FacultyItem>>();
        return structure!.Single(f => f.Code == code);
    }

    [Fact]
    public async Task AddFaculty_CodeTakenInAnotherCase_IsRefusedUnderTheField()
    {
        var code = NewCode();
        await AddFacultyAsync(code);

        var response = await SendAsync(HttpMethod.Post, "/api/admin/faculties",
            new FacultyForm { Code = code.ToLowerInvariant(), Name = "Inny wydział " + code });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.True(problem!.Errors.ContainsKey(nameof(FacultyForm.Code)));
    }

    [Fact]
    public async Task MoveFieldOfStudy_ToAnotherFaculty_MovesItAndLogsIt()
    {
        var from = await AddFacultyAsync(NewCode());
        var to = await AddFacultyAsync(NewCode());

        var added = await SendAsync(HttpMethod.Post, "/api/admin/fields-of-study", new FieldOfStudyForm
        {
            Name = "Informatyka",
            Level = StudyLevelOption.FirstCycle,
            FacultyId = from.Id
        });
        var field = (await added.Content.ReadFromJsonAsync<List<FacultyItem>>())!
            .Single(f => f.Id == from.Id).FieldsOfStudy.Single();

        var response = await SendAsync(HttpMethod.Put, $"/api/admin/fields-of-study/{field.Id}",
            new FieldOfStudyForm { Name = "Informatyka", Level = StudyLevelOption.FirstCycle, FacultyId = to.Id });

        response.EnsureSuccessStatusCode();
        var structure = (await response.Content.ReadFromJsonAsync<List<FacultyItem>>())!;
        Assert.Empty(structure.Single(f => f.Id == from.Id).FieldsOfStudy);
        Assert.Single(structure.Single(f => f.Id == to.Id).FieldsOfStudy);

        await using var db = _postgres.CreateContext();
        var entry = await db.AuditLogEntries
            .SingleAsync(e => e.ActorId == _adminId && e.Action == AuditAction.FieldOfStudyChanged);
        Assert.Equal($"Informatyka, studia pierwszego stopnia (wydział {from.Code} → {to.Code})", entry.Details);
    }

    // A closed faculty with an open field of study would still take new students
    [Fact]
    public async Task DeactivateFaculty_OnlyAfterItsFieldsOfStudy()
    {
        var faculty = await AddFacultyAsync(NewCode());
        var added = await SendAsync(HttpMethod.Post, "/api/admin/fields-of-study", new FieldOfStudyForm
        {
            Name = "Prawo",
            Level = StudyLevelOption.LongCycle,
            FacultyId = faculty.Id
        });
        var field = (await added.Content.ReadFromJsonAsync<List<FacultyItem>>())!
            .Single(f => f.Id == faculty.Id).FieldsOfStudy.Single();
        var off = new FacultyForm { Code = faculty.Code, Name = faculty.Name, IsActive = false };

        var refused = await SendAsync(HttpMethod.Put, $"/api/admin/faculties/{faculty.Id}", off);

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);

        (await SendAsync(HttpMethod.Put, $"/api/admin/fields-of-study/{field.Id}", new FieldOfStudyForm
        {
            Name = "Prawo",
            Level = StudyLevelOption.LongCycle,
            FacultyId = faculty.Id,
            IsActive = false
        })).EnsureSuccessStatusCode();
        var allowed = await SendAsync(HttpMethod.Put, $"/api/admin/faculties/{faculty.Id}", off);

        allowed.EnsureSuccessStatusCode();
        var structure = (await allowed.Content.ReadFromJsonAsync<List<FacultyItem>>())!;
        Assert.False(structure.Single(f => f.Id == faculty.Id).IsActive);
    }
}