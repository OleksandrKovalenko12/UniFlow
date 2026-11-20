using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UniFlow.Domain.Entities;
using UniFlow.Domain.Enums;
using UniFlow.Infrastructure.Identity;
using UniFlow.Infrastructure.Time;
using UniFlow.Shared.Applications;
using UniFlow.Shared.ApplicationTypes;
using UniFlow.Shared.Drafts;

namespace UniFlow.Tests.Infrastructure.Database;

// What differs from the group change: a document only for a new name, "something must change"
[Collection("Postgres")]
public sealed class PersonalDataChangeSubmissionTests : IAsyncLifetime
{
    // The test student is "Anna Testowa"
    private static readonly object NewName = new
    {
        changeName = true,
        newFirstName = "Anna",
        newLastName = "Nowa",
        reason = "Marriage"
    };

    private static readonly byte[] PdfFile = "%PDF-1.4 test"u8.ToArray();

    private readonly PostgresFixture _postgres;
    private readonly UniFlowApiFactory _factory;
    private HttpClient _client = null!;

    public PersonalDataChangeSubmissionTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        _factory = new UniFlowApiFactory(postgres.ConnectionString);
    }

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });

        await using var db = _postgres.CreateContext();

        if (!await db.ApplicationTypes.AnyAsync(t => t.Code == ApplicationTypeCodes.PersonalDataChange))
        {
            db.ApplicationTypes.Add(new ApplicationType(ApplicationTypeCodes.PersonalDataChange,
                "Zmiana danych osobowych", "Opis.", requiresDeanDecision: false, maxAttachments: 2));
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

    private Task<HttpResponseMessage> SendAsync(string userId, HttpMethod method, string url,
        HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.Add(TestAuthHandler.UserHeader, userId);
        return _client.SendAsync(request);
    }

    // A draft with one document - the way the form uploads it
    private async Task<Guid> DraftWithDocumentAsync(string userId, int enrollmentId)
    {
        var saved = await SendAsync(userId, HttpMethod.Put, $"/api/drafts/{ApplicationTypeCodes.PersonalDataChange}",
            JsonContent.Create(new SaveDraftRequest
            {
                EnrollmentId = enrollmentId,
                FormData = JsonSerializer.Serialize(NewName)
            }));
        var draft = (await saved.Content.ReadFromJsonAsync<DraftDetails>())!;

        var upload = await SendAsync(userId, HttpMethod.Post, $"/api/drafts/{draft.Id}/files",
            new MultipartFormDataContent { { new ByteArrayContent(PdfFile), "file", "akt-malzenstwa.pdf" } });
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);

        return draft.Id;
    }

    private Task<HttpResponseMessage> SubmitAsync(string userId, int enrollmentId, object form,
        Guid? draftId = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/enrollments/{enrollmentId}/applications/personal-data-change"
            + (draftId is null ? "" : $"?draftId={draftId}"))
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
    public async Task Submit_NewNameWithDocument_SavesApplicationWithAttachment()
    {
        var (userId, enrollmentId) = await CreateStudentAsync();
        var draftId = await DraftWithDocumentAsync(userId, enrollmentId);

        var response = await SubmitAsync(userId, enrollmentId, NewName, draftId);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var submitted = (await response.Content.ReadFromJsonAsync<SubmittedApplication>())!;

        await using var db = _postgres.CreateContext();
        var saved = await db.Applications.Include(a => a.Attachments).SingleAsync(a => a.Id == submitted.Id);
        Assert.Equal(1, saved.ActiveUploadCount);
    }

    [Fact]
    public async Task Submit_NewNameWithoutDocument_IsConflictAndTakesNoNumber()
    {
        var (userId, enrollmentId) = await CreateStudentAsync();
        var numberBefore = await LastNumberAsync();

        var response = await SubmitAsync(userId, enrollmentId, NewName);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("AttachmentRequired", body.GetProperty("code").GetString());
        Assert.Equal(numberBefore, await LastNumberAsync());
    }

    [Fact]
    public async Task Submit_SameNameAsNow_IsRejectedAndTakesNoNumber()
    {
        var (userId, enrollmentId) = await CreateStudentAsync();
        var draftId = await DraftWithDocumentAsync(userId, enrollmentId);
        var numberBefore = await LastNumberAsync();

        var response = await SubmitAsync(userId, enrollmentId,
            new { changeName = true, newFirstName = "Anna", newLastName = "Testowa", reason = "Marriage" }, draftId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(numberBefore, await LastNumberAsync());
    }

    [Fact]
    public async Task Submit_OnlyPhone_NeedsNoDocument()
    {
        var (userId, enrollmentId) = await CreateStudentAsync();

        var response = await SubmitAsync(userId, enrollmentId, new { changePhone = true, phone = "600 123 456" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}