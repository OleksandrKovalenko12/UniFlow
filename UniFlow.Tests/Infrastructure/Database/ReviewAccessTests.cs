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
using UniFlow.Shared.Drafts;
using UniFlow.Shared.Profile;

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
            // One field of study per faculty - a second application there uses the same one
            var field = await db.FieldsOfStudy.FirstOrDefaultAsync(f => f.FacultyId == faculty.Id)
                ?? new FieldOfStudy("Informatyka", StudyLevel.FirstCycle,
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

    private Task<HttpResponseMessage> PostAsync(string url, string userId, string roles,
    object? body = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body ?? new { })
        };
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

    // The whole path of a group change: dean's office -> vice-dean -> approved
    [Fact]
    public async Task DeanOfficeForwards_ViceDeanApproves()
    {
        var faculty = await CreateFacultyAsync();
        var submitted = await SubmitAsync(await CreateUserAsync(), faculty);
        var employee = await CreateUserAsync(staffOf: faculty);
        var viceDean = await CreateUserAsync(staffOf: faculty);
        var url = $"/api/applications/{submitted.Id}";

        Assert.Equal(HttpStatusCode.OK, (await PostAsync($"{url}/take", employee, DeanOffice)).StatusCode);

        var forwarded = await (await PostAsync($"{url}/forward", employee, DeanOffice,
            new { comment = "Proponuję zatwierdzić." })).Content.ReadFromJsonAsync<ApplicationDetails>();
        Assert.Equal(ApplicationState.ForwardedToDean, forwarded!.Status);
        Assert.True(forwarded.History[^1].IsInternal);

        var approved = await (await PostAsync($"{url}/approve", viceDean, RoleNames.ViceDean))
            .Content.ReadFromJsonAsync<ApplicationDetails>();
        Assert.Equal(ApplicationState.Approved, approved!.Status);
        Assert.Empty(approved.Actions);
    }

    // Once forwarded, the case belongs to the vice-dean
    [Fact]
    public async Task DeanOffice_CannotDecideAForwardedApplication()
    {
        var faculty = await CreateFacultyAsync();
        var submitted = await SubmitAsync(await CreateUserAsync(), faculty);
        var employee = await CreateUserAsync(staffOf: faculty);
        var url = $"/api/applications/{submitted.Id}";
        await PostAsync($"{url}/take", employee, DeanOffice);
        await PostAsync($"{url}/forward", employee, DeanOffice);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await PostAsync($"{url}/approve", employee, DeanOffice)).StatusCode);
    }

    [Fact]
    public async Task RequestCorrection_WithoutComment_IsRejected()
    {
        var faculty = await CreateFacultyAsync();
        var submitted = await SubmitAsync(await CreateUserAsync(), faculty);
        var employee = await CreateUserAsync(staffOf: faculty);
        var url = $"/api/applications/{submitted.Id}";
        await PostAsync($"{url}/take", employee, DeanOffice);

        var response = await PostAsync($"{url}/request-correction", employee, DeanOffice,
            new { comment = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task StaffOfAnotherFaculty_CannotDecide()
    {
        var submitted = await SubmitAsync(await CreateUserAsync(), await CreateFacultyAsync());
        var outsider = await CreateUserAsync(staffOf: await CreateFacultyAsync());

        Assert.Equal(HttpStatusCode.NotFound,
            (await PostAsync($"/api/applications/{submitted.Id}/take", outsider, DeanOffice)).StatusCode);
    }

    // Filters of the dean's office list

    [Fact]
    public async Task List_AssignedToMe_ShowsOnlyMyCases()
    {
        var faculty = await CreateFacultyAsync();
        var mine = await SubmitAsync(await CreateUserAsync(), faculty);
        var colleagues = await SubmitAsync(await CreateUserAsync(), faculty);
        var me = await CreateUserAsync(staffOf: faculty);
        var colleague = await CreateUserAsync(staffOf: faculty);
        await PostAsync($"/api/applications/{mine.Id}/take", me, DeanOffice);
        await PostAsync($"/api/applications/{colleagues.Id}/take", colleague, DeanOffice);

        var page = await (await GetAsync("/api/review?tab=InReview&assigned=me", me, DeanOffice))
            .Content.ReadFromJsonAsync<PagedList<ReviewListItem>>();

        var item = Assert.Single(page!.Items);
        Assert.Equal(mine.Number, item.Number);
        Assert.True(item.AssignedToMe);
    }

    [Fact]
    public async Task List_SearchByApplicationNumber_FindsIt()
    {
        var faculty = await CreateFacultyAsync();
        var submitted = await SubmitAsync(await CreateUserAsync(), faculty);
        var employee = await CreateUserAsync(staffOf: faculty);

        var page = await (await GetAsync(
                $"/api/review?tab=New&search={Uri.EscapeDataString(submitted.Number)}", employee, DeanOffice))
            .Content.ReadFromJsonAsync<PagedList<ReviewListItem>>();

        Assert.Equal(submitted.Number, Assert.Single(page!.Items).Number);
    }

    // The vice-dean gets a case only once it has been forwarded to them
    [Fact]
    public async Task ViceDean_SeesOnlyForwardedApplications()
    {
        var faculty = await CreateFacultyAsync();
        var submitted = await SubmitAsync(await CreateUserAsync(), faculty);
        var employee = await CreateUserAsync(staffOf: faculty);
        var viceDean = await CreateUserAsync(staffOf: faculty);
        var details = $"/api/applications/{submitted.Id}";

        Assert.Equal(HttpStatusCode.NotFound,
            (await GetAsync(details, viceDean, RoleNames.ViceDean)).StatusCode);

        await PostAsync($"{details}/take", employee, DeanOffice);
        await PostAsync($"{details}/forward", employee, DeanOffice);

        Assert.Equal(HttpStatusCode.OK, (await GetAsync(details, viceDean, RoleNames.ViceDean)).StatusCode);
    }

    // The student's own applications
    [Fact]
    public async Task Mine_ListsOnlyTheStudentsOwnApplications()
    {
        var student = await CreateUserAsync();
        var own = await SubmitAsync(student, await CreateFacultyAsync());
        var someoneElses = await SubmitAsync(await CreateUserAsync(), await CreateFacultyAsync());

        var mine = await (await GetAsync("/api/applications/mine", student, RoleNames.Student))
            .Content.ReadFromJsonAsync<List<MyApplicationItem>>();

        Assert.Contains(mine!, a => a.Number == own.Number);
        Assert.DoesNotContain(mine!, a => a.Number == someoneElses.Number);
    }

    [Fact]
    public async Task Withdraw_ByTheAuthor_ClosesTheCase_OnlyOnce()
    {
        var student = await CreateUserAsync();
        var submitted = await SubmitAsync(student, await CreateFacultyAsync());
        var url = $"/api/applications/{submitted.Id}/withdraw";

        var withdrawn = await (await PostAsync(url, student, RoleNames.Student))
            .Content.ReadFromJsonAsync<ApplicationDetails>();
        Assert.Equal(ApplicationState.Withdrawn, withdrawn!.Status);
        Assert.False(withdrawn.CanWithdraw);

        Assert.Equal(HttpStatusCode.Conflict, (await PostAsync(url, student, RoleNames.Student)).StatusCode);
    }

    [Fact]
    public async Task Withdraw_SomeoneElsesApplication_IsNotFound()
    {
        var submitted = await SubmitAsync(await CreateUserAsync(), await CreateFacultyAsync());
        var otherStudent = await CreateUserAsync();

        Assert.Equal(HttpStatusCode.NotFound,
            (await PostAsync($"/api/applications/{submitted.Id}/withdraw", otherStudent, RoleNames.Student))
                .StatusCode);
    }

    [Fact]
    public async Task Student_CannotOpenTheStaffList()
    {
        var student = await CreateUserAsync();

        Assert.Equal(HttpStatusCode.Forbidden,
            (await GetAsync("/api/review", student, RoleNames.Student)).StatusCode);
    }

    // Correction: the student's changes wait in a correction draft and go in one save

    private static readonly object CorrectedForm = new
    {
        scope = "General",
        groupType = "Laboratory",
        currentGroup = "3",
        requestedGroup = "5",
        justification = "Kolizja z pracą - zaświadczenie w załączniku."
    };

    private async Task SendBackAsync(Guid applicationId, string employee)
    {
        var url = $"/api/applications/{applicationId}";
        Assert.Equal(HttpStatusCode.OK, (await PostAsync($"{url}/take", employee, DeanOffice)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PostAsync($"{url}/request-correction", employee, DeanOffice,
            new { comment = "Dołącz zaświadczenie." })).StatusCode);
    }

    private async Task<DraftDetails> SaveCorrectionAsync(Guid applicationId, string student,
        params Guid[] removed)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/drafts/correction/{applicationId}")
        {
            Content = JsonContent.Create(new SaveCorrectionRequest
            {
                FormData = System.Text.Json.JsonSerializer.Serialize(CorrectedForm),
                RemovedAttachmentIds = [.. removed]
            })
        };
        request.Headers.Add(TestAuthHandler.UserHeader, student);
        request.Headers.Add(TestAuthHandler.RolesHeader, RoleNames.Student);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<DraftDetails>())!;
    }

    private async Task UploadToDraftAsync(Guid draftId, string student)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/drafts/{draftId}/files")
        {
            Content = new MultipartFormDataContent
            {
                { new ByteArrayContent("%PDF-1.4 test"u8.ToArray()), "file", "zaswiadczenie.pdf" }
            }
        };
        request.Headers.Add(TestAuthHandler.UserHeader, student);
        request.Headers.Add(TestAuthHandler.RolesHeader, RoleNames.Student);
        Assert.Equal(HttpStatusCode.Created, (await _client.SendAsync(request)).StatusCode);
    }

    private Task<HttpResponseMessage> ResubmitAsync(Guid applicationId, string student) =>
        PostAsync($"/api/applications/{applicationId}/group-change/resubmit", student, RoleNames.Student,
            CorrectedForm);

    [Fact]
    public async Task Resubmit_AddsAndArchivesFiles_WithANewDocument_InOneSave()
    {
        var faculty = await CreateFacultyAsync();
        var student = await CreateUserAsync();
        var employee = await CreateUserAsync(staffOf: faculty);
        var submitted = await SubmitAsync(student, faculty);

        // First correction: a new file and a changed form
        await SendBackAsync(submitted.Id, employee);
        var draft = await SaveCorrectionAsync(submitted.Id, student);
        await UploadToDraftAsync(draft.Id, student);
        Assert.Equal(HttpStatusCode.OK, (await ResubmitAsync(submitted.Id, student)).StatusCode);

        Guid fileId;
        await using (var db = _postgres.CreateContext())
        {
            var application = await db.Applications.Include(a => a.Attachments)
                .SingleAsync(a => a.Id == submitted.Id);

            Assert.Equal(ApplicationStatus.Resubmitted, application.Status);
            Assert.Contains("\"requestedGroup\": \"5\"", application.FormData);
            fileId = Assert.Single(application.Attachments, a => a.Kind == AttachmentKind.StudentUpload).Id;
            // The new PDF; the first one stays, archived
            Assert.Equal(2, application.Attachments.Count(a => a.Kind == AttachmentKind.GeneratedDocument));
            Assert.Single(application.Attachments, a => a.Kind == AttachmentKind.GeneratedDocument && !a.IsArchived);
            Assert.False(await db.Drafts.AnyAsync(d => d.ApplicationId == submitted.Id));
        }

        // Second correction: that file removed - archived, not deleted
        await SendBackAsync(submitted.Id, employee);
        await SaveCorrectionAsync(submitted.Id, student, fileId);
        Assert.Equal(HttpStatusCode.OK, (await ResubmitAsync(submitted.Id, student)).StatusCode);

        await using (var db = _postgres.CreateContext())
        {
            var file = await db.Applications.Where(a => a.Id == submitted.Id)
                .SelectMany(a => a.Attachments).SingleAsync(a => a.Id == fileId);
            Assert.True(file.IsArchived);
        }
    }

    [Fact]
    public async Task Resubmit_SomeoneElsesApplication_IsNotFound()
    {
        var faculty = await CreateFacultyAsync();
        var submitted = await SubmitAsync(await CreateUserAsync(), faculty);
        await SendBackAsync(submitted.Id, await CreateUserAsync(staffOf: faculty));

        var response = await ResubmitAsync(submitted.Id, await CreateUserAsync());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Resubmit_NotSentBack_IsConflict()
    {
        var student = await CreateUserAsync();
        var submitted = await SubmitAsync(student, await CreateFacultyAsync());

        var response = await ResubmitAsync(submitted.Id, student);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // "Mój profil": the user's own data only

    [Fact]
    public async Task Profile_Student_ShowsAlbumNumberAndStudies()
    {
        var student = await CreateUserAsync();
        await SubmitAsync(student, await CreateFacultyAsync());

        var profile = await (await GetAsync("/api/profile", student, RoleNames.Student))
            .Content.ReadFromJsonAsync<ProfileDetails>();

        Assert.Equal([RoleNames.Student], profile!.Roles);
        Assert.NotNull(profile.AlbumNumber);
        Assert.Single(profile.Studies);
        Assert.Empty(profile.Faculties);
    }

    [Fact]
    public async Task Profile_Employee_ShowsFacultiesWithoutStudentData()
    {
        var faculty = await CreateFacultyAsync();
        var employee = await CreateUserAsync(staffOf: faculty);

        var profile = await (await GetAsync("/api/profile", employee, DeanOffice))
            .Content.ReadFromJsonAsync<ProfileDetails>();

        Assert.Equal([DeanOffice], profile!.Roles);
        Assert.Null(profile.AlbumNumber);
        Assert.Empty(profile.Studies);
        Assert.Equal([faculty.Name], profile.Faculties);
    }
}