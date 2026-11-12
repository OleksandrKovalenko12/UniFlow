using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using UniFlow.Domain.Entities;
using UniFlow.Domain.Enums;
using UniFlow.Infrastructure.Identity;
using UniFlow.Shared;
using UniFlow.Shared.Applications;
using UniFlow.Shared.ApplicationTypes;
using UniFlow.Shared.Review;

namespace UniFlow.Tests.Infrastructure.Database;

// Staff see the applications of their own faculties - and nothing else
[Collection("Postgres")]
public sealed class ReviewAccessTests : IAsyncLifetime
{
    private const string DeanOffice = RoleNames.DeanOffice;

    private readonly PostgresFixture _postgres;
    private readonly UniFlowApiFactory _factory;
    private HttpClient _client = null!;

    public ReviewAccessTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        _factory = new UniFlowApiFactory(postgres.ConnectionString);
    }

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

        await using var db = _postgres.CreateContext();

        if (!await db.ApplicationTypes.AnyAsync(t => t.Code == ApplicationTypeCodes.GroupChange))
        {
            db.ApplicationTypes.Add(new ApplicationType(ApplicationTypeCodes.GroupChange,
                "Zmiana grupy", "Opis.", requiresDeanDecision: true, maxAttachments: 2));
            await db.SaveChangesAsync();
        }
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private async Task<Faculty> CreateFacultyAsync()
    {
        await using var db = _postgres.CreateContext();
        var code = Guid.NewGuid().ToString("N")[..8];
        var faculty = new Faculty(code, $"Wydział {code}");
        db.Faculties.Add(faculty);
        await db.SaveChangesAsync();
        return faculty;
    }

    private async Task<string> CreateUserAsync(Faculty? staffOf = null)
    {
        await using var db = _postgres.CreateContext();
        var id = Guid.NewGuid().ToString("N");

        db.Users.Add(new ApplicationUser
        {
            Id = id,
            UserName = id,
            Email = $"{id}@test.local",
            FirstName = "Jan",
            LastName = "Testowy",
            CreatedAt = DateTime.UtcNow,
        });

        if (staffOf is not null)
            db.StaffAssignments.Add(new StaffAssignment(id, db.Faculties.Attach(staffOf).Entity));

        await db.SaveChangesAsync();
        return id;
    }

    // Gives the user studies at the faculty and submits a group change there
    private async Task<SubmittedApplication> SubmitAsync(string userId, Faculty faculty)
    {
        int enrollmentId;
        await using (var db = _postgres.CreateContext())
        {
            var field = new FieldOfStudy("Informatyka", StudyLevel.FirstCycle,
                db.Faculties.Attach(faculty).Entity);
            var enrollment = new Enrollment(userId, field, 3, StudyMode.FullTime);
            db.Enrollments.Add(enrollment);
            db.StudentProfiles.Add(new StudentProfile(userId,
                Random.Shared.Next(1_000_000, 9_999_999).ToString()));
            await db.SaveChangesAsync();
            enrollmentId = enrollment.Id;
        }

        var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/enrollments/{enrollmentId}/applications/group-change")
        {
            Content = JsonContent.Create(new
            {
                scope = "General",
                groupType = "Laboratory",
                currentGroup = "3",
                requestedGroup = "4",
                justification = "Kolizja z pracą."
            })
        };
        request.Headers.Add(TestAuthHandler.UserHeader, userId);
        request.Headers.Add(SubmissionHeaders.IdempotencyKey, Guid.NewGuid().ToString());

        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SubmittedApplication>())!;
    }

    private Task<HttpResponseMessage> GetAsync(string url, string userId, string roles)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add(TestAuthHandler.UserHeader, userId);
        request.Headers.Add(TestAuthHandler.RolesHeader, roles);
        return _client.SendAsync(request);
    }

    private async Task<List<string>> ListNumbersAsync(string userId, string roles)
    {
        var response = await GetAsync("/api/review?tab=New", userId, roles);
        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<PagedList<ReviewListItem>>();
        return page!.Items.Select(i => i.Number).ToList();
    }

    [Fact]
    public async Task StaffOfTheFaculty_SeesTheApplicationAndMayTakeIt()
    {
        var faculty = await CreateFacultyAsync();
        var submitted = await SubmitAsync(await CreateUserAsync(), faculty);
        var employee = await CreateUserAsync(staffOf: faculty);

        Assert.Contains(submitted.Number, await ListNumbersAsync(employee, DeanOffice));

        var details = await (await GetAsync($"/api/applications/{submitted.Id}", employee, DeanOffice))
            .Content.ReadFromJsonAsync<ApplicationDetails>();
        Assert.Equal([ReviewAction.Take], details!.Actions);
    }

    [Fact]
    public async Task StaffOfAnotherFaculty_SeesNothing()
    {
        var submitted = await SubmitAsync(await CreateUserAsync(), await CreateFacultyAsync());
        var outsider = await CreateUserAsync(staffOf: await CreateFacultyAsync());

        Assert.DoesNotContain(submitted.Number, await ListNumbersAsync(outsider, DeanOffice));
        Assert.Equal(HttpStatusCode.NotFound,
            (await GetAsync($"/api/applications/{submitted.Id}", outsider, DeanOffice)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await GetAsync($"/api/applications/{submitted.Id}/document", outsider, DeanOffice)).StatusCode);
    }

    // A student working in the dean's office of their own faculty
    [Fact]
    public async Task OwnApplication_IsNotOnTheStaffList_AndOpensAsTheStudentsView()
    {
        var faculty = await CreateFacultyAsync();
        var studentWorker = await CreateUserAsync(staffOf: faculty);
        var submitted = await SubmitAsync(studentWorker, faculty);
        var roles = $"{RoleNames.Student},{DeanOffice}";

        Assert.DoesNotContain(submitted.Number, await ListNumbersAsync(studentWorker, roles));

        var details = await (await GetAsync($"/api/applications/{submitted.Id}", studentWorker, roles))
            .Content.ReadFromJsonAsync<ApplicationDetails>();
        Assert.Empty(details!.Actions);
    }

    [Fact]
    public async Task Student_CannotOpenTheStaffList()
    {
        var student = await CreateUserAsync();

        Assert.Equal(HttpStatusCode.Forbidden,
            (await GetAsync("/api/review", student, RoleNames.Student)).StatusCode);
    }
}