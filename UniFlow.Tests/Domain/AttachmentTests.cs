using UniFlow.Domain;
using UniFlow.Domain.Entities;
using UniFlow.Domain.Enums;

namespace UniFlow.Tests.Domain;

public class AttachmentTests
{
    private const string StudentId = "student-1";
    private static readonly DateTime Now = new(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Actor Student = new(StudentId, ActorRole.Student);

    private static readonly StoredFile Pdf =
        new("akt.pdf", "application/pdf", "files/a.pdf", null, 1000);
    private static readonly StoredFile Image =
        new("skan.jpg", "image/jpeg", "files/b.jpg", "files/b.pdf", 2000);

    private static Application Submit()
    {
        var faculty = new Faculty("WI", "Wydział Informatyki");
        var field = new FieldOfStudy("Informatyka", StudyLevel.FirstCycle, faculty);
        var enrollment = new Enrollment(StudentId, field, 3, StudyMode.FullTime);
        var type = new ApplicationType("GROUP_CHANGE", "Zmiana grupy", "Opis typu.", true, 5);

        return new Application("UF/2026/0001", StudentId, enrollment, type, "{}", Now);
    }

    //Valid data

    [Fact]
    public void Constructor_ValidPdf_SetsProperties()
    {
        var attachment = Submit().AddAttachment(Student,
            Pdf with { OriginalFileName = "  akt.pdf  " }, Now);

        Assert.NotEqual(Guid.Empty, attachment.Id);
        Assert.Equal(AttachmentKind.StudentUpload, attachment.Kind);
        Assert.Equal("akt.pdf", attachment.OriginalFileName);
        Assert.Equal("application/pdf", attachment.OriginalContentType);
        Assert.Equal("files/a.pdf", attachment.StoragePath);
        Assert.Null(attachment.PdfStoragePath);
        Assert.Equal(1000, attachment.SizeBytes);
        Assert.Equal(StudentId, attachment.UploadedById);
        Assert.Equal(Now, attachment.UploadedAt);
        Assert.False(attachment.IsArchived);
        Assert.Null(attachment.ArchivedAt);
    }

    [Fact]
    public void Constructor_ValidImage_KeepsPdfVersionPath()
    {
        var attachment = Submit().AddAttachment(Student, Image, Now);

        Assert.Equal("files/b.jpg", attachment.StoragePath);
        Assert.Equal("files/b.pdf", attachment.PdfStoragePath);
    }

    //Content type

    [Theory]
    [InlineData("image/heic")]
    [InlineData("image/webp")]
    [InlineData("image/gif")]
    [InlineData("text/plain")]
    public void Constructor_UnsupportedContentType_Throws(string contentType)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Submit().AddAttachment(Student, Image with { ContentType = contentType }, Now));

        Assert.Equal("file", ex.ParamName);
    }

    [Fact]
    public void Constructor_PdfWithSeparatePdfVersion_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Submit().AddAttachment(Student, Pdf with { PdfStoragePath = "files/a2.pdf" }, Now));

        Assert.Equal("file", ex.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_NonPdfWithoutPdfVersion_Throws(string? pdfStoragePath)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Submit().AddAttachment(Student, Image with { PdfStoragePath = pdfStoragePath }, Now));

        Assert.Equal("file", ex.ParamName);
    }

    [Fact]
    public void Constructor_GeneratedDocumentNotPdf_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Submit().AttachGeneratedDocument(Image, Now));

        Assert.Equal("file", ex.ParamName);
    }

    //Size

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(Attachment.MaxSizeBytes + 1)]
    public void Constructor_InvalidSize_Throws(long size)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            Submit().AddAttachment(Student, Pdf with { SizeBytes = size }, Now));

        Assert.Equal("file", ex.ParamName);
    }

    [Fact]
    public void Constructor_SizeAtMax_IsAccepted()
    {
        var attachment = Submit().AddAttachment(Student,
            Pdf with { SizeBytes = Attachment.MaxSizeBytes }, Now);

        Assert.Equal(Attachment.MaxSizeBytes, attachment.SizeBytes);
    }

    //File name and path

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyFileName_Throws(string fileName)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Submit().AddAttachment(Student, Pdf with { OriginalFileName = fileName }, Now));

        Assert.Equal("file", ex.ParamName);
    }

    [Fact]
    public void Constructor_FileNameAtMaxLength_IsAccepted()
    {
        var fileName = new string('a', 251) + ".pdf";   // 255

        var attachment = Submit().AddAttachment(Student,
            Pdf with { OriginalFileName = fileName }, Now);

        Assert.Equal(fileName, attachment.OriginalFileName);
    }

    [Fact]
    public void Constructor_FileNameTooLong_Throws()
    {
        var fileName = new string('a', 252) + ".pdf";   // 256

        var ex = Assert.Throws<ArgumentException>(() =>
            Submit().AddAttachment(Student, Pdf with { OriginalFileName = fileName }, Now));

        Assert.Equal("file", ex.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyStoragePath_Throws(string storagePath)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Submit().AddAttachment(Student, Pdf with { StoragePath = storagePath }, Now));

        Assert.Equal("file", ex.ParamName);
    }

    //Time

    [Fact]
    public void Constructor_NonUtcTime_Throws()
    {
        var localTime = DateTime.SpecifyKind(Now, DateTimeKind.Local);

        var ex = Assert.Throws<ArgumentException>(() =>
            Submit().AddAttachment(Student, Pdf, localTime));

        Assert.Equal("uploadedAt", ex.ParamName);
    }
}