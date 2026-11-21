using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UniFlow.Domain.Entities;
using UniFlow.Domain.Enums;
using UniFlow.Infrastructure.Identity;
using UniFlow.Infrastructure.Time;
using UniFlow.Shared;
using UniFlow.Shared.Applications;
using UniFlow.Shared.ApplicationTypes;

namespace UniFlow.Tests.Infrastructure.Database;

// What differs from the group change: a document for some kinds, only this and the next year
[Collection("Postgres")]
public sealed class DeanLeaveSubmissionTests : IAsyncLifetime
{
    private static readonly int ThisYear = AcademicYear.StartYear(PolishTime.FromUtc(DateTime.UtcNow));

    private readonly PostgresFixture _postgres;
    private readonly UniFlowApiFactory _factory;
    private HttpClient _client = null!;

    public DeanLeaveSubmissionTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        _factory = new UniFlowApiFactory(postgres.ConnectionString);
    }

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

        await using var db = _postgres.CreateContext();

        if (!await db.ApplicationTypes.AnyAsync(t => t.Code == ApplicationTypeCodes.DeanLeave))
        {
            db.ApplicationTypes.Add(new ApplicationType(ApplicationTypeCodes.DeanLeave,
                "Urlop dziekański", "Opis.", requiresDeanDecision: true, maxAttachments: 5));
            await db.SaveChangesAsync();
        }
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private async Task<(string UserId, int EnrollmentId)> CreateStudentAsync()
    {
        await using var db = _postgres.CreateContext();

        var suffix = Guid.NewGuid().ToString("N");
        var user = new ApplicationUser
        {
            Id = suffix,
            UserName = suffix,
            Email = $"{suffix}@test.local",
            FirstName = "Anna",
            LastName = "Testowa",
            CreatedAt = DateTime.UtcNow,
        };

        var faculty = new Faculty(suffix[..8], $"Wydział {suffix}");
        var field = new FieldOfStudy("Informatyka", StudyLevel.FirstCycle, faculty);
        var enrollment = new Enrollment(user.Id, field, 3, StudyMode.FullTime);

        db.Users.Add(user);
        db.StudentProfiles.Add(new StudentProfile(user.Id, Random.Shared.Next(1_000_000, 9_999_999).ToString()));
        db.Enrollments.Add(enrollment);
        await db.SaveChangesAsync();

        return (user.Id, enrollment.Id);
    }

    private static object Leave(string kind, int startYear) => new
    {
        kind,
        period = "SummerSemester",
        academicYear = AcademicYear.Name(startYear),
        justification = "Leczenie szpitalne."
    };

    private Task<HttpResponseMessage> SubmitAsync(string userId, int enrollmentId, object form)
    {
        var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/enrollments/{enrollmentId}/applications/dean-leave")
        {
            Content = JsonContent.Create(form)
        };
        request.Headers.Add(TestAuthHandler.UserHeader, userId);
        request.Headers.Add(SubmissionHeaders.IdempotencyKey, Guid.NewGuid().ToString());

        return _client.SendAsync(request);
    }

    private async Task<int> LastNumberAsync()
    {
        await using var db = _postgres.CreateContext();
        var year = PolishTime.FromUtc(DateTime.UtcNow).Year;

        return await db.ApplicationNumberCounters
            .Where(c => c.Year == year)
            .Select(c => c.LastNumber)
            .SingleOrDefaultAsync();
    }

    [Fact]
    public async Task Submit_OccasionalWithoutDocument_IsAllowed()
    {
        var (userId, enrollmentId) = await CreateStudentAsync();

        var response = await SubmitAsync(userId, enrollmentId, Leave("Occasional", ThisYear));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Submit_HealthWithoutDocument_IsConflictAndTakesNoNumber()
    {
        var (userId, enrollmentId) = await CreateStudentAsync();
        var numberBefore = await LastNumberAsync();

        var response = await SubmitAsync(userId, enrollmentId, Leave("Health", ThisYear + 1));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("AttachmentRequired", body.GetProperty("code").GetString());
        Assert.Equal(numberBefore, await LastNumberAsync());
    }

    [Fact]
    public async Task Submit_TwoYearsAhead_IsRejectedAndTakesNoNumber()
    {
        var (userId, enrollmentId) = await CreateStudentAsync();
        var numberBefore = await LastNumberAsync();

        var response = await SubmitAsync(userId, enrollmentId, Leave("Occasional", ThisYear + 2));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(numberBefore, await LastNumberAsync());
    }
}