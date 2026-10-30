using UniFlow.Domain;
using UniFlow.Domain.Entities;
using UniFlow.Domain.Enums;

namespace UniFlow.Tests.Domain;

public class ApplicationTests
{
    private const string StudentId = "student-1";
    private static readonly DateTime Now = new(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc);
    private static Enrollment CreateEnrollment(string studentId = StudentId)
    {
        var faculty = new Faculty("WI", "Wydział Informatyki");
        var field = new FieldOfStudy("Informatyka", StudyLevel.FirstCycle, faculty);
        return new Enrollment(studentId, field, 3, StudyMode.FullTime);
    }

    private static ApplicationType CreateType(bool requiresDeanDecision = true,
        int maxAttachments = 5) =>
        new("GROUP_CHANGE", "Zmiana grupy", "Opis typu.", requiresDeanDecision, maxAttachments);

    private static Application Submit(
        Enrollment? enrollment = null,
        ApplicationType? type = null,
        string applicantId = StudentId) =>
        new("UF/2026/0001", applicantId, enrollment ?? CreateEnrollment(), type ?? CreateType(),
            "{}", Now);

    //Files
    private static readonly StoredFile Pdf =
        new("akt.pdf", "application/pdf", "files/a.pdf", null, 1000);
    private static readonly StoredFile Image =
        new("skan.jpg", "image/jpeg", "files/b.jpg", "files/b.pdf", 2000);

    //Actors
    private static readonly Actor Student = new(StudentId, ActorRole.Student);
    private static readonly Actor OtherStudent = new("student-2", ActorRole.Student);
    private static readonly Actor DeanOffice = new("dean-office-1", ActorRole.DeanOffice);
    private static readonly Actor ViceDean = new("vice-dean-1", ActorRole.ViceDean);

    //Gets new application to the given status in the shortest way.
    private static Application CreateInStatus(ApplicationStatus status,
        bool requiresDeanDecision = true)
    {
        var app = Submit(type: CreateType(requiresDeanDecision));

        switch (status)
        {
            case ApplicationStatus.Submitted:
                break;
            case ApplicationStatus.UnderReview:
                app.TakeForReview(DeanOffice, Now);
                break;
            case ApplicationStatus.NeedsCorrection:
                app.TakeForReview(DeanOffice, Now);
                app.RequestCorrection(DeanOffice, "Missing signature.", Now);
                break;
            case ApplicationStatus.Resubmitted:
                app.TakeForReview(DeanOffice, Now);
                app.RequestCorrection(DeanOffice, "Missing signature.", Now);
                app.Resubmit(Student, "{\"fixed\":true}", Now);
                break;
            case ApplicationStatus.ForwardedToDean:
                app.TakeForReview(DeanOffice, Now);
                app.ForwardToDean(DeanOffice, null, Now);
                break;
            case ApplicationStatus.Approved:
                app.TakeForReview(DeanOffice, Now);
                app.ForwardToDean(DeanOffice, null, Now);
                app.Approve(ViceDean, Now);
                break;
            case ApplicationStatus.Rejected:
                app.TakeForReview(DeanOffice, Now);
                app.Reject(DeanOffice, "Submitted after deadline.", Now);
                break;
            case ApplicationStatus.Withdrawn:
                app.Withdraw(Student, Now);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status));
        }
        return app;
    }

    //Right submissions
    [Fact]
    public void Constructor_ValidData_CreatesSubmittedApplication()
    {
        var application = Submit();

        Assert.NotEqual(Guid.Empty, application.Id);
        Assert.Equal("UF/2026/0001", application.Number);
        Assert.Equal(StudentId, application.ApplicantId);
        Assert.Equal(ApplicationStatus.Submitted, application.Status);
        Assert.Null(application.AssignedToId);
        Assert.Equal(Now, application.CreatedAt);
    }

    [Fact]
    public void Constructor_CreatesFirstHistoryEntry()
    {
        var application = Submit();

        var entry = Assert.Single(application.StatusHistory);
        Assert.Null(entry.FromStatus);
        Assert.Equal(ApplicationStatus.Submitted, entry.ToStatus);
        Assert.Equal(StudentId, entry.ChangedById);
        Assert.Equal(Now, entry.ChangedAt);
        Assert.False(entry.IsInternal);
    }

    [Fact]
    public void Constructor_CopiesRequiresDeanDecision_ChangeOfTypeDoesNotAffectApplication()
    {
        var type = CreateType(requiresDeanDecision: false);
        var application = Submit(type: type);

        type.SetRequiresDeanDecision(true);   // regulations changed

        Assert.False(application.RequiresDeanDecision);
    }

    [Fact]
    public void Constructor_CopiesMaxAttachments_ChangeOfTypeDoesNotAffectApplication()
    {
        var type = CreateType(maxAttachments: 3);
        var application = Submit(type: type);

        type.SetMaxAttachments(1);   // admin lowered the limit

        Assert.Equal(3, application.MaxAttachments);
    }

    //Business rules
    [Fact]
    public void Constructor_OtherStudentsEnrollment_Throws()
    {
        var enrollmentOfOtherStudent = CreateEnrollment("student-2");

        var ex = Assert.Throws<DomainException>(() => 
            Submit(enrollment: enrollmentOfOtherStudent, applicantId: StudentId));
        Assert.Equal(DomainErrorCode.NotOwnEnrollment, ex.Code);
    }

    [Fact]
    public void Constructor_InactiveEnrollment_Throws()
    {
        var enrollment = CreateEnrollment();
        enrollment.Deactivate();

        var ex = Assert.Throws<DomainException>(() => Submit(enrollment: enrollment));
        Assert.Equal(DomainErrorCode.EnrollmentInactive, ex.Code);
    }

    [Fact]
    public void Constructor_InactiveType_Throws()
    {
        var type = CreateType();
        type.Deactivate();

        var ex = Assert.Throws<DomainException>(() => Submit(type: type));
        Assert.Equal(DomainErrorCode.ApplicationTypeInactive, ex.Code);
    }

    //Data validation
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_EmptyFormData_Throws(string? formData)
    {
        Assert.ThrowsAny<ArgumentException>(
            () => new Application("UF/2026/0001", StudentId, CreateEnrollment(), CreateType(),
                formData!, Now));
    }

    [Fact]
    public void Constructor_LocalTime_Throws()
    {
        var localTime = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Local);

        Assert.Throws<ArgumentException>(
            () => new Application("UF/2026/0001", StudentId, CreateEnrollment(), CreateType(),
                "{}", localTime));
    }

    //Allowed status changes
    [Fact]
    public void TakeForReview_FromSubmitted_ChangesStatusAndAssignsWorker()
    {
        var app = CreateInStatus(ApplicationStatus.Submitted);

        app.TakeForReview(DeanOffice, Now);

        Assert.Equal(ApplicationStatus.UnderReview, app.Status);
        Assert.Equal(DeanOffice.Id, app.AssignedToId);
    }

    [Fact]
    public void TakeForReview_FromResubmitted_ChangesStatus()
    {
        var app = CreateInStatus(ApplicationStatus.Resubmitted);

        app.TakeForReview(DeanOffice, Now);

        Assert.Equal(ApplicationStatus.UnderReview, app.Status);
    }

    [Fact]
    public void RequestCorrection_AddsPublicTrimmedComment()
    {
        var app = CreateInStatus(ApplicationStatus.UnderReview);

        app.RequestCorrection(DeanOffice, "  Missing signature.  ", Now);

        Assert.Equal(ApplicationStatus.NeedsCorrection, app.Status);
        var entry = app.StatusHistory.Last();
        Assert.Equal("Missing signature.", entry.Comment);
        Assert.False(entry.IsInternal);
    }

    [Fact]
    public void Resubmit_ByApplicant_UpdatesFormData()
    {
        var app = CreateInStatus(ApplicationStatus.NeedsCorrection);

        app.Resubmit(Student, "{\"new\":1}", Now);

        Assert.Equal(ApplicationStatus.Resubmitted, app.Status);
        Assert.Equal("{\"new\":1}", app.FormData);
    }

    [Fact]
    public void ForwardToDean_WithNote_CreatesInternalEntry()
    {
        var app = CreateInStatus(ApplicationStatus.UnderReview);

        app.ForwardToDean(DeanOffice, "2 free places.", Now);

        Assert.Equal(ApplicationStatus.ForwardedToDean, app.Status);
        Assert.True(app.StatusHistory.Last().IsInternal);
    }

    [Fact]
    public void ForwardToDean_WithoutNote_CreatesPublicEntry()
    {
        var app = CreateInStatus(ApplicationStatus.UnderReview);

        app.ForwardToDean(DeanOffice, null, Now);

        Assert.False(app.StatusHistory.Last().IsInternal);
    }

    [Fact]
    public void Approve_ShortPath_ByDeanOffice()
    {
        var app = CreateInStatus(ApplicationStatus.UnderReview, requiresDeanDecision: false);

        app.Approve(DeanOffice, Now);

        Assert.Equal(ApplicationStatus.Approved, app.Status);
    }

    [Fact]
    public void Approve_LongPath_ByViceDean()
    {
        var app = CreateInStatus(ApplicationStatus.ForwardedToDean);

        app.Approve(ViceDean, Now);

        Assert.Equal(ApplicationStatus.Approved, app.Status);
    }

    [Theory]
    [InlineData(ApplicationStatus.UnderReview)]
    [InlineData(ApplicationStatus.NeedsCorrection)]
    public void Reject_ByDeanOffice_FormalRejection(ApplicationStatus from)
    {
        var app = CreateInStatus(from);

        app.Reject(DeanOffice, "Submitted after deadline.", Now);

        Assert.Equal(ApplicationStatus.Rejected, app.Status);
    }

    [Fact]
    public void Reject_ByViceDean_MeritRejection()
    {
        var app = CreateInStatus(ApplicationStatus.ForwardedToDean);

        app.Reject(ViceDean, "No explanation.", Now);

        Assert.Equal(ApplicationStatus.Rejected, app.Status);
    }

    [Fact]
    public void ReturnToDeanOffice_CreatesInternalEntry_KeepsAssignedWorker()
    {
        var app = CreateInStatus(ApplicationStatus.ForwardedToDean);

        app.ReturnToDeanOffice(ViceDean, "Please attach the teacher's opinion.", Now);

        Assert.Equal(ApplicationStatus.UnderReview, app.Status);
        Assert.True(app.StatusHistory.Last().IsInternal);
        Assert.Equal(DeanOffice.Id, app.AssignedToId);
    }

    [Theory]
    [InlineData(ApplicationStatus.Submitted)]
    [InlineData(ApplicationStatus.UnderReview)]
    [InlineData(ApplicationStatus.NeedsCorrection)]
    [InlineData(ApplicationStatus.Resubmitted)]
    public void Withdraw_ByApplicant_FromEarlyStatus(ApplicationStatus from)
    {
        var app = CreateInStatus(from);

        app.Withdraw(Student, Now);

        Assert.Equal(ApplicationStatus.Withdrawn, app.Status);
    }

    //Disallowed status changes
    [Fact]
    public void Approve_LongPath_ByDeanOffice_Throws()
    {
        var app = CreateInStatus(ApplicationStatus.UnderReview, requiresDeanDecision: true);

        var ex = Assert.Throws<DomainException>(() => app.Approve(DeanOffice, Now));
        Assert.Equal(DomainErrorCode.DeanDecisionRequired, ex.Code);
    }

    [Fact]
    public void ForwardToDean_ShortPath_Throws()
    {
        var app = CreateInStatus(ApplicationStatus.UnderReview, requiresDeanDecision: false);

        var ex = Assert.Throws<DomainException>(() => app.ForwardToDean(DeanOffice, null, Now));
        Assert.Equal(DomainErrorCode.DeanDecisionNotRequired, ex.Code);
    }

    [Fact]
    public void Approve_ByStudent_Throws()
    {
        var app = CreateInStatus(ApplicationStatus.ForwardedToDean);

        Assert.Throws<DomainException>(() => app.Approve(Student, Now));
    }

    [Fact]
    public void Approve_FromSubmitted_Throws()
    {
        var app = CreateInStatus(ApplicationStatus.Submitted);

        Assert.Throws<DomainException>(() => app.Approve(ViceDean, Now));
    }

    [Fact]
    public void Resubmit_ByOtherStudent_Throws()
    {
        var app = CreateInStatus(ApplicationStatus.NeedsCorrection);

        Assert.Throws<DomainException>(() => app.Resubmit(OtherStudent, "{}", Now));
    }

    [Fact]
    public void Withdraw_ByOtherStudent_Throws()
    {
        var app = CreateInStatus(ApplicationStatus.Submitted);

        Assert.Throws<DomainException>(() => app.Withdraw(OtherStudent, Now));
    }

    [Fact]
    public void TakeForReview_ByStaffWhoIsApplicant_Throws()
    {
        var app = CreateInStatus(ApplicationStatus.Submitted);
        var staffWhoIsApplicant = new Actor(StudentId, ActorRole.DeanOffice);

        var ex = Assert.Throws<DomainException>(() => app.TakeForReview(staffWhoIsApplicant, Now));
        Assert.Equal(DomainErrorCode.ConflictOfInterest, ex.Code);
    }

    [Theory]
    [InlineData(ApplicationStatus.ForwardedToDean)]
    [InlineData(ApplicationStatus.Approved)]
    [InlineData(ApplicationStatus.Rejected)]
    [InlineData(ApplicationStatus.Withdrawn)]
    public void Withdraw_FromLateOrFinalStatus_Throws(ApplicationStatus from)
    {
        var app = CreateInStatus(from);

        Assert.Throws<DomainException>(() => app.Withdraw(Student, Now));
    }

    [Theory]
    [InlineData(ApplicationStatus.Approved)]
    [InlineData(ApplicationStatus.Rejected)]
    [InlineData(ApplicationStatus.Withdrawn)]
    public void FinalStatus_CannotBeTakenForReview(ApplicationStatus final)
    {
        var app = CreateInStatus(final);

        Assert.Throws<DomainException>(() => app.TakeForReview(DeanOffice, Now));
    }

    //Comments tests
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Reject_WithoutComment_Throws(string comment)
    {
        var app = CreateInStatus(ApplicationStatus.UnderReview);

        Assert.Throws<ArgumentException>(() => app.Reject(DeanOffice, comment, Now));
    }

    [Fact]
    public void RequestCorrection_TooLongComment_Throws_AndStatusIsUnchanged()
    {
        var app = CreateInStatus(ApplicationStatus.UnderReview);
        var historyCountBefore = app.StatusHistory.Count;

        Assert.Throws<ArgumentException>(
            () => app.RequestCorrection(DeanOffice, new string('x', 2001), Now));

        Assert.Equal(ApplicationStatus.UnderReview, app.Status);
        Assert.Equal(historyCountBefore, app.StatusHistory.Count);
    }

    //Full path
    [Fact]
    public void FullLongPath_HistoryRecordsEveryStep()
    {
        var app = Submit(type: CreateType(requiresDeanDecision: true));

        app.TakeForReview(DeanOffice, Now);
        app.ForwardToDean(DeanOffice, "There are free places.", Now);
        app.ReturnToDeanOffice(ViceDean, "Please attach the teacher's opinion", Now);
        app.ForwardToDean(DeanOffice, "Teacher agreed.", Now);
        app.Approve(ViceDean, Now);

        var statuses = app.StatusHistory.Select(e => e.ToStatus).ToArray();
        Assert.Equal(new[]
        {
            ApplicationStatus.Submitted,
            ApplicationStatus.UnderReview,
            ApplicationStatus.ForwardedToDean,
            ApplicationStatus.UnderReview,
            ApplicationStatus.ForwardedToDean,
            ApplicationStatus.Approved
        }, statuses);
    }

    //Attachments: AddAttachment
    [Theory]
    [InlineData(ApplicationStatus.Submitted)]
    [InlineData(ApplicationStatus.NeedsCorrection)]
    public void AddAttachment_InAllowedStatus_AddsAttachment(ApplicationStatus status)
    {
        var app = CreateInStatus(status);

        var attachment = app.AddAttachment(Student, Image, Now);

        Assert.Same(attachment, Assert.Single(app.Attachments));
    }

    [Theory]
    [InlineData(ApplicationStatus.UnderReview)]
    [InlineData(ApplicationStatus.Resubmitted)]
    [InlineData(ApplicationStatus.ForwardedToDean)]
    [InlineData(ApplicationStatus.Approved)]
    [InlineData(ApplicationStatus.Rejected)]
    [InlineData(ApplicationStatus.Withdrawn)]
    public void AddAttachment_InOtherStatus_Throws(ApplicationStatus status)
    {
        var app = CreateInStatus(status);

        Assert.Throws<DomainException>(() => app.AddAttachment(Student, Pdf, Now));
    }

    [Fact]
    public void AddAttachment_OtherStudent_Throws()
    {
        var app = Submit();

        var ex = Assert.Throws<DomainException>(() => app.AddAttachment(OtherStudent, Pdf, Now));
        Assert.Equal(DomainErrorCode.NotApplicant, ex.Code);
    }

    [Fact]
    public void AddAttachment_StaffMember_Throws()
    {
        var app = Submit();

        var ex = Assert.Throws<DomainException>(() => app.AddAttachment(DeanOffice, Pdf, Now));
        Assert.Equal(DomainErrorCode.RoleNotAllowed, ex.Code);
    }

    [Fact]
    public void AddAttachment_LimitReached_Throws()
    {
        var app = Submit(type: CreateType(maxAttachments: 2));
        app.AddAttachment(Student, Pdf, Now);
        app.AddAttachment(Student, Image, Now);

        var ex = Assert.Throws<DomainException>(() => app.AddAttachment(Student, Pdf, Now));
        Assert.Equal(DomainErrorCode.AttachmentLimitReached, ex.Code);
    }

    [Fact]
    public void AddAttachment_ZeroLimit_Throws()
    {
        var app = Submit(type: CreateType(maxAttachments: 0));

        Assert.Throws<DomainException>(() => app.AddAttachment(Student, Pdf, Now));
    }

    [Fact]
    public void AddAttachment_ArchivedDoesNotCountToLimit()
    {
        var app = Submit(type: CreateType(maxAttachments: 1));
        var first = app.AddAttachment(Student, Pdf, Now);
        app.ArchiveAttachment(Student, first.Id, Now);

        app.AddAttachment(Student, Image, Now);

        Assert.Equal(2, app.Attachments.Count);
    }

    [Fact]
    public void AddAttachment_GeneratedDocumentDoesNotCountToLimit()
    {
        var app = Submit(type: CreateType(maxAttachments: 1));
        app.AttachGeneratedDocument(Pdf, Now);

        app.AddAttachment(Student, Image, Now);

        Assert.Equal(2, app.Attachments.Count);
    }

    [Fact]
    public void AddAttachment_UpdatesUpdatedAt()
    {
        var app = Submit();
        var later = Now.AddHours(2);

        app.AddAttachment(Student, Pdf, later);

        Assert.Equal(later, app.UpdatedAt);
    }

    //Attachments: ArchiveAttachment

    [Fact]
    public void ArchiveAttachment_Valid_ArchivesButKeepsFile()
    {
        var app = Submit();
        var attachment = app.AddAttachment(Student, Pdf, Now);
        var later = Now.AddHours(1);

        app.ArchiveAttachment(Student, attachment.Id, later);

        Assert.True(attachment.IsArchived);
        Assert.Equal(later, attachment.ArchivedAt);
        Assert.Single(app.Attachments);   // archived, not deleted
    }

    [Fact]
    public void ArchiveAttachment_UnknownId_Throws()
    {
        var app = Submit();

        Assert.Throws<DomainException>(() =>
            app.ArchiveAttachment(Student, Guid.NewGuid(), Now));
    }

    [Fact]
    public void ArchiveAttachment_AttachmentOfOtherApplication_Throws()
    {
        var otherApp = Submit();
        var foreignAttachment = otherApp.AddAttachment(Student, Pdf, Now);
        var app = Submit();

        var ex = Assert.Throws<DomainException>(() =>
            app.ArchiveAttachment(Student, foreignAttachment.Id, Now));
        Assert.Equal(DomainErrorCode.AttachmentNotFound, ex.Code);
        Assert.False(foreignAttachment.IsArchived);
    }

    [Fact]
    public void ArchiveAttachment_GeneratedDocument_Throws()
    {
        var app = Submit();
        var document = app.AttachGeneratedDocument(Pdf, Now);

        var ex = Assert.Throws<DomainException>(() =>
            app.ArchiveAttachment(Student, document.Id, Now));
        Assert.Equal(DomainErrorCode.AttachmentNotArchivable, ex.Code);
        Assert.False(document.IsArchived);
    }

    [Fact]
    public void ArchiveAttachment_AlreadyArchived_Throws()
    {
        var app = Submit();
        var attachment = app.AddAttachment(Student, Pdf, Now);
        app.ArchiveAttachment(Student, attachment.Id, Now);

        var ex = Assert.Throws<DomainException>(() =>
            app.ArchiveAttachment(Student, attachment.Id, Now));
        Assert.Equal(DomainErrorCode.AttachmentAlreadyArchived, ex.Code);
    }

    [Fact]
    public void ArchiveAttachment_OtherStudent_Throws()
    {
        var app = Submit();
        var attachment = app.AddAttachment(Student, Pdf, Now);

        Assert.Throws<DomainException>(() =>
            app.ArchiveAttachment(OtherStudent, attachment.Id, Now));
    }

    [Fact]
    public void ArchiveAttachment_DuringReview_Throws()
    {
        var app = Submit();
        var attachment = app.AddAttachment(Student, Pdf, Now);
        app.TakeForReview(DeanOffice, Now);

        Assert.Throws<DomainException>(() =>
            app.ArchiveAttachment(Student, attachment.Id, Now));
        Assert.False(attachment.IsArchived);
    }

    //Attachments: AttachGeneratedDocument

    [Fact]
    public void AttachGeneratedDocument_First_AddsActiveDocument()
    {
        var app = Submit();

        var document = app.AttachGeneratedDocument(Pdf, Now);

        Assert.Same(document, Assert.Single(app.Attachments));
        Assert.Equal(AttachmentKind.GeneratedDocument, document.Kind);
        Assert.Equal(StudentId, document.UploadedById);
        Assert.False(document.IsArchived);
    }

    [Fact]
    public void AttachGeneratedDocument_Again_ArchivesPrevious()
    {
        var app = Submit();
        var first = app.AttachGeneratedDocument(Pdf, Now);

        var second = app.AttachGeneratedDocument(Pdf, Now.AddHours(1));

        Assert.True(first.IsArchived);
        Assert.False(second.IsArchived);
        Assert.Single(app.Attachments, a =>
            a.Kind == AttachmentKind.GeneratedDocument && !a.IsArchived);
    }

    [Fact]
    public void AttachGeneratedDocument_InvalidFile_KeepsPreviousActive()
    {
        var app = Submit();
        var first = app.AttachGeneratedDocument(Pdf, Now);

        Assert.Throws<ArgumentException>(() => app.AttachGeneratedDocument(Image, Now));

        Assert.False(first.IsArchived);
        Assert.Single(app.Attachments);
    }

    [Theory]
    [InlineData(ApplicationStatus.Submitted)]
    [InlineData(ApplicationStatus.Resubmitted)]
    public void AttachGeneratedDocument_InAllowedStatus_AddsDocument(ApplicationStatus status)
    {
        var app = CreateInStatus(status);

        app.AttachGeneratedDocument(Pdf, Now);

        Assert.Single(app.Attachments);
    }

    [Theory]
    [InlineData(ApplicationStatus.UnderReview)]
    [InlineData(ApplicationStatus.NeedsCorrection)]
    [InlineData(ApplicationStatus.ForwardedToDean)]
    [InlineData(ApplicationStatus.Approved)]
    [InlineData(ApplicationStatus.Rejected)]
    [InlineData(ApplicationStatus.Withdrawn)]
    public void AttachGeneratedDocument_InOtherStatus_Throws(ApplicationStatus status)
    {
        var app = CreateInStatus(status);

        Assert.Throws<DomainException>(() => app.AttachGeneratedDocument(Pdf, Now));
    }
}
