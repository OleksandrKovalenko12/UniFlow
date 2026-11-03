using Microsoft.Extensions.Logging.Abstractions;
using UniFlow.Documents.Models;
using UniFlow.Documents.Rendering;
using UniFlow.Documents.Templates;
using UniFlow.Infrastructure.Pdf;
using UniFlow.Shared.Forms;

namespace UniFlow.Tests.Documents;

public class DocumentPdfManualTests
{
    private const string ManualReason =
        "Manual check: requires running Gotenberg (docker compose up -d).";
    private const string OutputFolder = @"C:\uniflow-samples\output\documents";

    private static readonly ApplicationHeader Header = new(
        ApplicationNumber: "UF/2026/0001",
        SubmittedAt: new DateTime(2026, 11, 2, 9, 41, 0),
        StudentFullName: "Anna Testowa",
        AlbumNumber: "100001",
        FacultyName: "Wydział Informatyki",
        FieldOfStudy: "Informatyka",
        StudyForm: "studia stacjonarne, I stopnia",
        Semester: 7,
        AcademicYear: "2026/2027");

    private static readonly Dictionary<string, GroupChangeForm> GroupChangeForms = new()
    {
        ["group-change-general"] = new GroupChangeForm
        {
            Scope = GroupChangeScope.General,
            GroupType = GroupType.Laboratory,
            CurrentGroup = "3",
            RequestedGroup = "4",
            Justification = "Od października pracuję w godzinach zajęć grupy nr 3.\nW załączniku zaświadczenie od pracodawcy."
        },
        ["group-change-subjects"] = new GroupChangeForm
        {
            Scope = GroupChangeScope.Subjects,
            Subjects =
            [
                new SubjectGroupChange { Subject = "Bazy danych", GroupType = GroupType.Laboratory, CurrentGroup = "L2", RequestedGroup = "L4" },
                new SubjectGroupChange { Subject = "Sieci komputerowe", GroupType = GroupType.Exercises, CurrentGroup = "Ć1", RequestedGroup = "Ć2" }
            ],
            Justification = "Zajęcia kolidują z zajęciami na drugim kierunku studiów."
        }
    };

    //(Skip = ManualReason)

    [Fact(Skip = ManualReason)]
    public async Task RenderGroupChangeVariantsToPdf()
    {
        var renderer = new DocumentRenderer(NullLoggerFactory.Instance);
        var converter = new GotenbergPdfConverter(new HttpClient
        {
            BaseAddress = new Uri("http://localhost:3000/"),
            Timeout = TimeSpan.FromSeconds(60)
        });

        Directory.CreateDirectory(OutputFolder);

        foreach (var (name, form) in GroupChangeForms)
        {
            var html = await renderer.RenderAsync<GroupChangeTemplate, GroupChangeDocument>(
                new GroupChangeDocument(Header, form));
            var pdf = await converter.ConvertHtmlToPdfAsync(html);

            Assert.Equal("%PDF"u8.ToArray(), pdf[..4]);

            await File.WriteAllBytesAsync(Path.Combine(OutputFolder, name + ".pdf"), pdf);
        }
    }
}