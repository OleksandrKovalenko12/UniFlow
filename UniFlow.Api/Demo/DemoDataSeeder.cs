using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UniFlow.Api.Applications;
using UniFlow.Documents.Models;
using UniFlow.Documents.Rendering;
using UniFlow.Documents.Templates;
using UniFlow.Domain;
using UniFlow.Domain.Entities;
using UniFlow.Domain.Enums;
using UniFlow.Infrastructure.Identity;
using UniFlow.Infrastructure.Pdf;
using UniFlow.Infrastructure.Persistence;
using UniFlow.Infrastructure.Storage;
using UniFlow.Infrastructure.Time;
using UniFlow.Shared;
using UniFlow.Shared.ApplicationTypes;
using UniFlow.Shared.Forms;

namespace UniFlow.Api.Demo;

// Fills an empty development database with the stories of DemoStories - so every account has
// something to show at once. Everything goes through the real steps: submission with a number
// and a PDF, the Domain's state machine, drafts with files. A story the rules do not allow
// stops it - so it checks the rules too.
// Not one transaction: the application number is taken outside a transaction on purpose
// (ApplicationNumberGenerator). Gotenberg is checked first, so it rarely stops halfway.
public sealed class DemoDataSeeder
{
    private const string AdminEmail = "adam.nowak@uniflow.test";

    private readonly UniFlowDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationSubmissionService _submissions;
    private readonly DocumentRenderer _renderer;
    private readonly IPdfConverter _pdf;
    private readonly IFileStorage _storage;
    private readonly ILogger<DemoDataSeeder> _logger;

    private readonly DateTime _now = DateTime.UtcNow;
    private readonly Dictionary<string, string> _userIds = [];
    private byte[] _sampleFile = [];

    public DemoDataSeeder(UniFlowDbContext db, UserManager<ApplicationUser> userManager,
        ApplicationSubmissionService submissions, DocumentRenderer renderer, IPdfConverter pdf,
        IFileStorage storage, ILogger<DemoDataSeeder> logger)
    {
        _db = db;
        _userManager = userManager;
        _submissions = submissions;
        _renderer = renderer;
        _pdf = pdf;
        _storage = storage;
        _logger = logger;
    }

    // Only into a database without applications - never over someone's own test data
    public async Task SeedAsync(string password, CancellationToken cancellationToken = default)
    {
        if (await _db.Applications.AnyAsync(cancellationToken))
            return;

        // Every application has a PDF - without Gotenberg there would be holes
        try
        {
            _sampleFile = await _pdf.ConvertHtmlToPdfAsync(SampleFileHtml, cancellationToken);
        }
        catch (PdfConversionException)
        {
            _logger.LogWarning("Demo data skipped: Gotenberg is not running. Start Docker, " +
                "drop the database and run the application again.");
            return;
        }

        try
        {
            await CreateStudentsAsync(password, cancellationToken);
            await PlayStoriesAsync(cancellationToken);
            await CreateDraftsAsync(cancellationToken);
            await FinishAccountsAsync(password, cancellationToken);
            await WriteAuditLogAsync(cancellationToken);
            await SetLoginTimesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Demo data stopped halfway. Drop the database and run the " +
                "application again.");
            throw;
        }

        _logger.LogInformation("Demo data created: {Count} applications.", DemoStories.Stories.Count);
    }

    // ---------- People

    private async Task CreateStudentsAsync(string password, CancellationToken cancellationToken)
    {
        foreach (var student in DemoStories.Students)
            await CreateStudentAsync(student, password, At(30, 9, 0), cancellationToken);
    }

    private async Task<ApplicationUser> CreateStudentAsync(DemoStudent student, string password,
        DateTime createdAt, CancellationToken cancellationToken)
    {
        var user = new ApplicationUser
        {
            UserName = student.Email,
            Email = student.Email,
            EmailConfirmed = true,
            FirstName = student.FirstName,
            LastName = student.LastName,
            CreatedAt = createdAt,
        };
        Check(await _userManager.CreateAsync(user, password));
        Check(await _userManager.AddToRoleAsync(user, RoleNames.Student));

        _db.StudentProfiles.Add(new StudentProfile(user.Id, student.AlbumNumber));
        foreach (var studies in student.Studies)
        {
            var enrollment = new Enrollment(user.Id, await FieldAsync(studies.FieldOfStudy, studies.Level,
                cancellationToken), studies.Semester, studies.Mode);
            _db.Enrollments.Add(enrollment);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return user;
    }

    // ---------- Applications

    // All steps of all stories in the order they happened - application numbers follow the dates
    private async Task PlayStoriesAsync(CancellationToken cancellationToken)
    {
        var steps = DemoStories.Stories
            .SelectMany((story, index) => story.Steps.Select(step => (Story: index, Step: step)))
            .OrderBy(s => At(s.Step.At))
            .ToList();

        var applications = new Dictionary<int, Guid>();

        foreach (var (index, step) in steps)
        {
            var story = DemoStories.Stories[index];
            var at = At(step.At);

            if (step.Action == DemoAction.Submit)
                applications[index] = await SubmitAsync(story, step, at, cancellationToken);
            else if (step.Action == DemoAction.Resubmit)
                await ResubmitAsync(story, step, applications[index], at, cancellationToken);
            else
                await DecideAsync(story, step, applications[index], at, cancellationToken);

            // Each step starts from the database, as a new request would
            _db.ChangeTracker.Clear();
        }
    }

    private async Task<Guid> SubmitAsync(DemoStory story, DemoStep step, DateTime at,
        CancellationToken cancellationToken)
    {
        var studentId = await UserIdAsync(story.Student);
        var enrollment = await EnrollmentAsync(studentId, story, cancellationToken);
        var form = Checked(story.Form);

        // Attachments come the student's way - through a draft whose files become them
        Guid? draftId = null;
        if (step.Files > 0)
        {
            var type = await GroupChangeTypeAsync(cancellationToken);
            var draft = new Draft(studentId, enrollment, type, FormJson.Serialize(form), at);
            await AddSampleFilesAsync(draft, step.Files, at, cancellationToken);
            _db.Drafts.Add(draft);
            await _db.SaveChangesAsync(cancellationToken);
            draftId = draft.Id;
        }

        var submitted = await _submissions.SubmitAsync(studentId, Guid.NewGuid(), enrollment.Id,
            ApplicationTypeCodes.GroupChange, FormJson.Serialize(form), draftId, RenderFor(form),
            cancellationToken, at);

        return submitted.Id;
    }

    private async Task ResubmitAsync(DemoStory story, DemoStep step, Guid applicationId, DateTime at,
        CancellationToken cancellationToken)
    {
        var studentId = await UserIdAsync(story.Student);
        var form = Checked(story.Form);
        if (step.Text is not null)
            form.Justification = step.Text;
        var formData = FormJson.Serialize(form);

        // New files and the file to take out - the correction draft, as on the correction page
        if (step.Files > 0 || step.ReplaceFirstFile)
        {
            var application = await _db.Applications
                .Include(a => a.Enrollment)
                .Include(a => a.ApplicationType)
                .Include(a => a.Attachments)
                .SingleAsync(a => a.Id == applicationId, cancellationToken);

            var draft = Draft.ForCorrection(application, formData, at);
            var removed = step.ReplaceFirstFile
                ? application.Attachments
                    .Where(a => a.Kind == AttachmentKind.StudentUpload && !a.IsArchived)
                    .OrderBy(a => a.UploadedAt)
                    .Take(1)
                    .Select(a => a.Id)
                    .ToList()
                : [];
            draft.UpdateCorrection(formData, removed, at);
            await AddSampleFilesAsync(draft, step.Files, at, cancellationToken);
            _db.Drafts.Add(draft);
            await _db.SaveChangesAsync(cancellationToken);
            _db.ChangeTracker.Clear();
        }

        await _submissions.ResubmitAsync(studentId, applicationId, ApplicationTypeCodes.GroupChange,
            formData, RenderFor(form), cancellationToken, at);
    }

    // The decisions - the same Domain methods the dean's office and the vice-dean use
    private async Task DecideAsync(DemoStory story, DemoStep step, Guid applicationId, DateTime at,
        CancellationToken cancellationToken)
    {
        var application = await _db.Applications.SingleAsync(a => a.Id == applicationId, cancellationToken);

        switch (step.Action)
        {
            case DemoAction.Take:
                application.TakeForReview(await DeanOfficeAsync(step), at);
                break;
            case DemoAction.RequestCorrection:
                application.RequestCorrection(await DeanOfficeAsync(step), step.Text!, at);
                break;
            case DemoAction.Forward:
                application.ForwardToDean(await DeanOfficeAsync(step), step.Text, at);
                break;
            case DemoAction.Approve:
                application.Approve(await ActorForAsync(step, application), at);
                break;
            case DemoAction.Reject:
                application.Reject(await ActorForAsync(step, application), step.Text!, at);
                break;
            case DemoAction.ReturnToDeanOffice:
                application.ReturnToDeanOffice(new Actor(await UserIdAsync(step.By!), ActorRole.ViceDean),
                    step.Text!, at);
                break;
            case DemoAction.Withdraw:
                application.Withdraw(new Actor(await UserIdAsync(story.Student), ActorRole.Student), at);
                break;
            default:
                throw new InvalidOperationException($"Unknown demo step {step.Action}.");
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    // ---------- Drafts, accounts, log, logins

    private async Task CreateDraftsAsync(CancellationToken cancellationToken)
    {
        var type = await GroupChangeTypeAsync(cancellationToken);

        foreach (var demo in DemoStories.Drafts)
        {
            var studentId = await UserIdAsync(demo.Student);
            var enrollment = await EnrollmentAsync(studentId, demo.FieldOfStudy, demo.Level, cancellationToken);
            var at = At(demo.At);

            var draft = new Draft(studentId, enrollment, type, FormJson.Serialize(demo.Form), at);
            await AddSampleFilesAsync(draft, demo.Files, at, cancellationToken);
            _db.Drafts.Add(draft);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    // The graduate's studies end and the account is switched off; a newcomer without their own
    // password; a field of study being phased out
    private async Task FinishAccountsAsync(string password, CancellationToken cancellationToken)
    {
        foreach (var student in DemoStories.Students.Where(s => s.Studies.Any(studies => studies.Ended)))
        {
            var userId = await UserIdAsync(student.Email);
            var ended = await _db.Enrollments.Include(e => e.FieldOfStudy)
                .Where(e => e.StudentId == userId)
                .ToListAsync(cancellationToken);
            foreach (var enrollment in ended)
                enrollment.Deactivate();

            var user = (await _userManager.FindByIdAsync(userId))!;
            user.IsActive = false;
            Check(await _userManager.UpdateAsync(user));
        }

        var newcomer = await CreateStudentAsync(DemoStories.NewcomerWithoutPassword, password, At(1, 14, 20),
            cancellationToken);
        newcomer.MustChangePassword = true;
        Check(await _userManager.UpdateAsync(newcomer));

        var phasedOut = await FieldAsync("Teleinformatyka", StudyLevel.FirstCycle, cancellationToken);
        phasedOut.Deactivate();

        await _db.SaveChangesAsync(cancellationToken);
    }

    // What the administrator would have left behind doing all of the above by hand
    private async Task WriteAuditLogAsync(CancellationToken cancellationToken)
    {
        var adminId = await UserIdAsync(AdminEmail);
        var entries = new List<AuditLogEntry>();

        void Log(AuditAction action, string? target, string? details, DateTime at) =>
            entries.Add(new AuditLogEntry(adminId, action, target, details, at));

        var minute = 0;
        foreach (var student in DemoStories.Students)
        {
            var id = await UserIdAsync(student.Email);
            var at = At(30, 9, minute += 3);
            Log(AuditAction.UserCreated, id, student.Email, at);
            Log(AuditAction.RoleAssigned, id, RoleNames.Student, at);
        }

        Log(AuditAction.StaffAssigned, await UserIdAsync("joanna.szymanska@uniflow.test"), "WPiA", At(29, 10, 15));
        Log(AuditAction.ApplicationTypeChanged, null, "Urlop dziekański (limit załączników 3 → 5)", At(26, 12, 5));
        Log(AuditAction.FieldOfStudyChanged, null, "Teleinformatyka, studia pierwszego stopnia (wyłączony)",
            At(24, 8, 30));
        Log(AuditAction.PersonalDataCorrected, await UserIdAsync("s100011@student.uniflow.test"),
            "Julia Wieczorek → Julia Wieczorek-Lis", At(2, 11, 40));
        Log(AuditAction.UserDeactivated, await UserIdAsync("s100014@student.uniflow.test"), null, At(5, 13, 0));

        var newcomer = await UserIdAsync(DemoStories.NewcomerWithoutPassword.Email);
        Log(AuditAction.UserCreated, newcomer, DemoStories.NewcomerWithoutPassword.Email, At(1, 14, 20));
        Log(AuditAction.RoleAssigned, newcomer, RoleNames.Student, At(1, 14, 20));

        _db.AuditLogEntries.AddRange(entries);
        await _db.SaveChangesAsync(cancellationToken);
    }

    // Everyone last logged in two days ago - the start page shows what happened since
    private async Task SetLoginTimesAsync(CancellationToken cancellationToken)
    {
        var lastLogin = At(2, 16, 0);
        var newcomerEmail = DemoStories.NewcomerWithoutPassword.Email;

        await _db.Users
            .Where(u => u.IsActive && u.Email != newcomerEmail)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.PreviousLoginAt, At(5, 8, 30))
                .SetProperty(u => u.LastLoginAt, lastLogin), cancellationToken);
    }

    // ---------- Helpers

    // DaysAgo before today (Polish time), at a Polish hour - never later than now
    private DateTime At(DemoTime time) => At(time.DaysAgo, time.Hour, time.Minute);

    private DateTime At(int daysAgo, int hour, int minute)
    {
        var today = DateOnly.FromDateTime(PolishTime.FromUtc(_now));
        var at = PolishTime.StartOfDayUtc(today.AddDays(-daysAgo)) + new TimeSpan(hour, minute, 0);
        return at < _now ? at : _now.AddMinutes(-1);
    }

    private async Task<string> UserIdAsync(string email)
    {
        if (_userIds.TryGetValue(email, out var id))
            return id;

        var user = await _userManager.FindByEmailAsync(email)
            ?? throw new InvalidOperationException($"Demo data: no account {email}.");
        return _userIds[email] = user.Id;
    }

    private async Task<Actor> DeanOfficeAsync(DemoStep step) =>
        new(await UserIdAsync(step.By!), ActorRole.DeanOffice);

    // Forwarded cases are decided by the vice-dean, the rest by the dean's office
    private async Task<Actor> ActorForAsync(DemoStep step, Application application) =>
        new(await UserIdAsync(step.By!), application.Status == ApplicationStatus.ForwardedToDean
            ? ActorRole.ViceDean
            : ActorRole.DeanOffice);

    private Task<FieldOfStudy> FieldAsync(string name, StudyLevel level, CancellationToken cancellationToken) =>
        _db.FieldsOfStudy.SingleAsync(f => f.Name == name && f.Level == level, cancellationToken);

    private Task<Enrollment> EnrollmentAsync(string studentId, DemoStory story,
        CancellationToken cancellationToken) =>
        EnrollmentAsync(studentId, story.FieldOfStudy, story.Level, cancellationToken);

    private Task<Enrollment> EnrollmentAsync(string studentId, string field, StudyLevel level,
        CancellationToken cancellationToken) =>
        _db.Enrollments
            .Include(e => e.FieldOfStudy)
            .SingleAsync(e => e.StudentId == studentId && e.FieldOfStudy.Name == field
                && e.FieldOfStudy.Level == level, cancellationToken);

    private Task<ApplicationType> GroupChangeTypeAsync(CancellationToken cancellationToken) =>
        _db.ApplicationTypes.SingleAsync(t => t.Code == ApplicationTypeCodes.GroupChange, cancellationToken);

    // A made-up document, saved like a file a student would upload
    private async Task AddSampleFilesAsync(Draft draft, int count, DateTime at,
        CancellationToken cancellationToken)
    {
        for (var i = 1; i <= count; i++)
        {
            using var content = new MemoryStream(_sampleFile);
            var path = await _storage.SaveAsync(content, $"uploads/{draft.Id}", ".pdf", cancellationToken);
            draft.AddFile(new StoredFile($"Zaswiadczenie_{i}.pdf", Attachment.PdfContentType, path, null,
                _sampleFile.Length), at);
        }
    }

    private Func<ApplicationHeader, Task<string>> RenderFor(GroupChangeForm form) =>
        header => _renderer.RenderAsync<GroupChangeTemplate, GroupChangeDocument>(
            new GroupChangeDocument(header, form));

    // The same rules as the form on the page - a wrong story stops here, not in the PDF
    private static GroupChangeForm Checked(GroupChangeForm form)
    {
        var copy = FormJson.Deserialize<GroupChangeForm>(FormJson.Serialize(form));
        copy.Normalize();
        Validator.ValidateObject(copy, new ValidationContext(copy), validateAllProperties: true);
        return copy;
    }

    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException("Demo data: " +
                string.Join("; ", result.Errors.Select(e => e.Description)));
    }

    private const string SampleFileHtml = """
        <!DOCTYPE html>
        <html lang="pl"><head><meta charset="utf-8"></head>
        <body style="font-family: sans-serif; padding: 40px">
            <h1>Zaświadczenie</h1>
            <p>Dokument przykładowy wygenerowany na potrzeby demonstracji systemu UniFlow.</p>
            <p>Nie dotyczy żadnej rzeczywistej osoby.</p>
        </body></html>
        """;
}