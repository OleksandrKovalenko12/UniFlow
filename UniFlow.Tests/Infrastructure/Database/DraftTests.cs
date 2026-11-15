using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UniFlow.Api.Applications;
using UniFlow.Domain.Entities;
using UniFlow.Domain.Enums;
using UniFlow.Infrastructure.Identity;
using UniFlow.Shared.Applications;
using UniFlow.Shared.ApplicationTypes;
using UniFlow.Shared.Drafts;

namespace UniFlow.Tests.Infrastructure.Database;

[Collection("Postgres")]
public sealed class DraftTests : IAsyncLifetime
{
    private static readonly object ValidForm = new
    {
        scope = "General",
        groupType = "Laboratory",
        currentGroup = "3",
        requestedGroup = "4",
        justification = "Kolizja z pracą."
    };

    // A PDF is stored as it is - the fake converter is not needed
    private static readonly byte[] PdfFile = "%PDF-1.4 test"u8.ToArray();

    private readonly PostgresFixture _postgres;
    private readonly UniFlowApiFactory _factory;
    private HttpClient _client = null!;

    public DraftTests(PostgresFixture postgres)
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

    private Task<HttpResponseMessage> SendAsync(string userId, HttpMethod method, string url,
        HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.Add(TestAuthHandler.UserHeader, userId);
        return _client.SendAsync(request);
    }

    private async Task<DraftDetails> SaveDraftAsync(string userId, int enrollmentId, object form)
    {
        var response = await SendAsync(userId, HttpMethod.Put, $"/api/drafts/{ApplicationTypeCodes.GroupChange}",
            JsonContent.Create(new SaveDraftRequest
            {
                EnrollmentId = enrollmentId,
                FormData = JsonSerializer.Serialize(form)
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<DraftDetails>())!;
    }

    private Task<HttpResponseMessage> UploadAsync(string userId, Guid draftId) =>
        SendAsync(userId, HttpMethod.Post, $"/api/drafts/{draftId}/files", new MultipartFormDataContent
        {
            { new ByteArrayContent(PdfFile), "file", "zaswiadczenie.pdf" }
        });

    private int StoredFileCount() => Directory.Exists(_factory.StorageRoot)
        ? Directory.GetFiles(_factory.StorageRoot, "*", SearchOption.AllDirectories).Length
        : 0;

    [Fact]
    public async Task Save_Twice_KeepsOneDraftWithTheLatestForm()
    {
        var (userId, enrollmentId) = await CreateStudentAsync();

        var first = await SaveDraftAsync(userId, enrollmentId, new { scope = "General" });
        var second = await SaveDraftAsync(userId, enrollmentId, ValidForm);

        Assert.Equal(first.Id, second.Id);

        await using var db = _postgres.CreateContext();
        var saved = Assert.Single(await db.Drafts.Where(d => d.StudentId == userId).ToListAsync());
        Assert.Contains("Kolizja", saved.FormData);
    }

    [Fact]
    public async Task Submit_WithDraft_FileBecomesAttachmentAndDraftIsDeleted()
    {
        var (userId, enrollmentId) = await CreateStudentAsync();
        var draft = await SaveDraftAsync(userId, enrollmentId, ValidForm);
        Assert.Equal(HttpStatusCode.Created, (await UploadAsync(userId, draft.Id)).StatusCode);

        var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/enrollments/{enrollmentId}/applications/group-change?draftId={draft.Id}")
        {
            Content = JsonContent.Create(ValidForm)
        };
        request.Headers.Add(TestAuthHandler.UserHeader, userId);
        request.Headers.Add(SubmissionHeaders.IdempotencyKey, Guid.NewGuid().ToString());
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var submitted = (await response.Content.ReadFromJsonAsync<SubmittedApplication>())!;

        await using var db = _postgres.CreateContext();
        var application = await db.Applications.Include(a => a.Attachments)
            .SingleAsync(a => a.Id == submitted.Id);

        var upload = Assert.Single(application.Attachments, a => a.Kind == AttachmentKind.StudentUpload);
        Assert.Equal("zaswiadczenie.pdf", upload.OriginalFileName);
        Assert.False(await db.Drafts.AnyAsync(d => d.Id == draft.Id));
        // The file is kept - it belongs to the application now
        Assert.True(File.Exists(Path.Combine(_factory.StorageRoot, upload.StoragePath)));
    }

    [Fact]
    public async Task SomeoneElsesDraft_LooksMissing()
    {
        var (ownerId, enrollmentId) = await CreateStudentAsync();
        var (otherId, _) = await CreateStudentAsync();
        var draft = await SaveDraftAsync(ownerId, enrollmentId, ValidForm);
        var file = await (await UploadAsync(ownerId, draft.Id)).Content.ReadFromJsonAsync<DraftFileInfo>();
        var fileId = file!.Id;

        Assert.Equal(HttpStatusCode.NotFound, (await UploadAsync(otherId, draft.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await SendAsync(otherId, HttpMethod.Get, $"/api/drafts/{draft.Id}/files/{fileId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await SendAsync(otherId, HttpMethod.Delete, $"/api/drafts/{draft.Id}")).StatusCode);

        await using var db = _postgres.CreateContext();
        Assert.True(await db.Drafts.AnyAsync(d => d.Id == draft.Id));
    }

    [Fact]
    public async Task DeleteExpired_RemovesOnlyDraftsOlderThanRetention()
    {
        var (userId, enrollmentId) = await CreateStudentAsync();
        var draft = await SaveDraftAsync(userId, enrollmentId, ValidForm);
        await UploadAsync(userId, draft.Id);
        var filesBefore = StoredFileCount();

        await using var scope = _factory.Services.CreateAsyncScope();
        var drafts = scope.ServiceProvider.GetRequiredService<DraftService>();

        await drafts.DeleteExpiredAsync(DateTime.UtcNow.AddDays(Draft.RetentionDays - 1), default);
        await using (var db = _postgres.CreateContext())
            Assert.True(await db.Drafts.AnyAsync(d => d.Id == draft.Id));

        await drafts.DeleteExpiredAsync(DateTime.UtcNow.AddDays(Draft.RetentionDays + 1), default);
        await using (var db = _postgres.CreateContext())
            Assert.False(await db.Drafts.AnyAsync(d => d.Id == draft.Id));
        Assert.Equal(filesBefore - 1, StoredFileCount());
    }
}