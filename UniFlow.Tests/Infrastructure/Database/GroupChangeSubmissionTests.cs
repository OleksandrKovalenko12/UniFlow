using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using UniFlow.Domain.Entities;
using UniFlow.Domain.Enums;
using UniFlow.Infrastructure.Identity;
using UniFlow.Infrastructure.Time;
using UniFlow.Shared.Applications;
using UniFlow.Shared.ApplicationTypes;

namespace UniFlow.Tests.Infrastructure.Database;

[Collection("Postgres")]
public sealed class GroupChangeSubmissionTests : IAsyncLifetime
{
    private static readonly object ValidForm = new
    {
        scope = "General",
        groupType = "Laboratory",
        currentGroup = "3",
        requestedGroup = "4",
        justification = "Kolizja z pracą."
    };

    private readonly PostgresFixture _postgres;
    private readonly UniFlowApiFactory _factory;
    private HttpClient _client = null!;

    public GroupChangeSubmissionTests(PostgresFixture postgres)
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

    // A new student for every test, with one active enrollment
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

    private Task<HttpResponseMessage> SubmitAsync(string userId, int enrollmentId, object form,
        Guid? submissionKey = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/enrollments/{enrollmentId}/applications/group-change")
        {
            Content = JsonContent.Create(form)
        };
        request.Headers.Add(TestAuthHandler.UserHeader, userId);
        request.Headers.Add(SubmissionHeaders.IdempotencyKey, (submissionKey ?? Guid.NewGuid()).ToString());

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

    private async Task<int> ApplicationCountAsync(string userId)
    {
        await using var db = _postgres.CreateContext();
        return await db.Applications.CountAsync(a => a.ApplicantId == userId);
    }

    private int StoredFileCount() => Directory.Exists(_factory.StorageRoot)
        ? Directory.GetFiles(_factory.StorageRoot, "*", SearchOption.AllDirectories).Length
        : 0;

    [Fact]
    public async Task Submit_ValidForm_SavesApplicationWithDocument()
    {
        var (userId, enrollmentId) = await CreateStudentAsync();

        var response = await SubmitAsync(userId, enrollmentId, ValidForm);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var submitted = await response.Content.ReadFromJsonAsync<SubmittedApplication>();

        await using var db = _postgres.CreateContext();
        var saved = await db.Applications
            .Include(a => a.StatusHistory)
            .Include(a => a.Attachments)
            .SingleAsync(a => a.Id == submitted!.Id);

        Assert.Equal(submitted!.Number, saved.Number);
        Assert.Equal(ApplicationStatus.Submitted, saved.Status);
        Assert.Single(saved.StatusHistory);
        Assert.Equal(AttachmentKind.GeneratedDocument, Assert.Single(saved.Attachments).Kind);
        Assert.Equal(1, StoredFileCount());
    }

    [Fact]
    public async Task Submit_SomeoneElsesEnrollment_IsForbiddenAndTakesNoNumber()
    {
        var (attackerId, _) = await CreateStudentAsync();
        var (_, victimEnrollmentId) = await CreateStudentAsync();
        var numberBefore = await LastNumberAsync();

        var response = await SubmitAsync(attackerId, victimEnrollmentId, ValidForm);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(numberBefore, await LastNumberAsync());
        Assert.Equal(0, await ApplicationCountAsync(attackerId));
    }

    [Fact]
    public async Task Submit_InvalidForm_IsRejectedAndTakesNoNumber()
    {
        var (userId, enrollmentId) = await CreateStudentAsync();
        var numberBefore = await LastNumberAsync();

        var response = await SubmitAsync(userId, enrollmentId, new
        {
            scope = "General",
            groupType = "Laboratory",
            currentGroup = "3",
            requestedGroup = "3",
            justification = "Kolizja z pracą."
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(numberBefore, await LastNumberAsync());
        Assert.Equal(0, await ApplicationCountAsync(userId));
    }

    private Task<HttpResponseMessage> GetDocumentAsync(string userId, Guid applicationId, string query = "")
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/applications/{applicationId}/document{query}");
        request.Headers.Add(TestAuthHandler.UserHeader, userId);

        return _client.SendAsync(request);
    }

    private async Task<SubmittedApplication> SubmitValidAsync(string userId, int enrollmentId)
    {
        var response = await SubmitAsync(userId, enrollmentId, ValidForm);
        return (await response.Content.ReadFromJsonAsync<SubmittedApplication>())!;
    }

    [Theory]
    [InlineData("", "attachment")]
    [InlineData("?inline=true", "inline")]
    public async Task GetDocument_Author_ReceivesPdf(string query, string expectedDisposition)
    {
        var (userId, enrollmentId) = await CreateStudentAsync();
        var submitted = await SubmitValidAsync(userId, enrollmentId);

        var response = await GetDocumentAsync(userId, submitted.Id, query);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal(expectedDisposition, response.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Contains(submitted.Number.Replace('/', '-'), response.Content.Headers.ContentDisposition.FileName);
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.Equal("%PDF-test"u8.ToArray(), await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task GetDocument_OtherStudent_IsNotFound()
    {
        var (authorId, enrollmentId) = await CreateStudentAsync();
        var (otherId, _) = await CreateStudentAsync();
        var submitted = await SubmitValidAsync(authorId, enrollmentId);

        var response = await GetDocumentAsync(otherId, submitted.Id);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }


    [Fact]
    public async Task Submit_PdfServiceDown_SavesNothing()
    {
        var (userId, enrollmentId) = await CreateStudentAsync();
        _factory.Pdf.Fail = true;

        var response = await SubmitAsync(userId, enrollmentId, ValidForm);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(0, await ApplicationCountAsync(userId));
        Assert.Equal(0, StoredFileCount());
    }

    [Fact]
    public async Task Submit_SameKeyTwice_ReturnsFirstApplication()
    {
        var (userId, enrollmentId) = await CreateStudentAsync();
        var key = Guid.NewGuid();

        var first = await SubmitAsync(userId, enrollmentId, ValidForm, key);
        var numberAfterFirst = await LastNumberAsync();
        var second = await SubmitAsync(userId, enrollmentId, ValidForm, key);

        Assert.Equal(await first.Content.ReadFromJsonAsync<SubmittedApplication>(),
            await second.Content.ReadFromJsonAsync<SubmittedApplication>());
        Assert.Equal(numberAfterFirst, await LastNumberAsync());
        Assert.Equal(1, await ApplicationCountAsync(userId));
        Assert.Equal(1, StoredFileCount());
    }

    [Fact]
    public async Task Submit_SameKeyAtOnce_CreatesOneApplication()
    {
        var (userId, enrollmentId) = await CreateStudentAsync();
        var key = Guid.NewGuid();
        _factory.Pdf.Delay = TimeSpan.FromMilliseconds(300);

        var responses = await Task.WhenAll(
            SubmitAsync(userId, enrollmentId, ValidForm, key),
            SubmitAsync(userId, enrollmentId, ValidForm, key));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        Assert.Equal(await responses[0].Content.ReadFromJsonAsync<SubmittedApplication>(),
            await responses[1].Content.ReadFromJsonAsync<SubmittedApplication>());
        Assert.Equal(1, await ApplicationCountAsync(userId));
        Assert.Equal(1, StoredFileCount());
    }

}