using Microsoft.Extensions.Logging.Abstractions;
using UniFlow.Documents.Models;
using UniFlow.Documents.Rendering;
using UniFlow.Documents.Templates;
using UniFlow.Shared.Forms;

namespace UniFlow.Tests.Documents;

public class PersonalDataChangeTemplateTests
{
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

    private static Task<string> RenderAsync(PersonalDataChangeForm form) =>
        new DocumentRenderer(NullLoggerFactory.Instance)
            .RenderAsync<PersonalDataChangeTemplate, PersonalDataChangeDocument>(
                new PersonalDataChangeDocument(Header, form, new CurrentPersonalData("Anna", "Testowa", null)));

    [Fact]
    public async Task Render_NewName_ShowsCurrentAndNewNameWithReason()
    {
        var html = await RenderAsync(new PersonalDataChangeForm
        {
            ChangeName = true,
            NewFirstName = "Anna",
            NewLastName = "Nowa",
            Reason = PersonalDataChangeReason.Marriage
        });

        Assert.Contains("<title>Podanie o zmianę danych osobowych</title>", html);
        Assert.Contains("<strong>Dziekanat</strong>", html);
        Assert.Contains("<tr><td>Nazwisko</td><td>Testowa</td><td>Nowa</td></tr>", html);
        Assert.Contains("zawarcie związku małżeńskiego", html);
    }

    [Fact]
    public async Task Render_OnlyPhone_ShowsOnlyThatRowAndNoDocumentSentence()
    {
        var html = await RenderAsync(new PersonalDataChangeForm
        {
            ChangePhone = true,
            OldPhone = "600111222",
            NewPhone = "600123456"
        });

        Assert.Contains("<tr><td>Telefon</td><td>600111222</td><td>600123456</td></tr>", html);
        Assert.DoesNotContain("Nazwisko", html);
        Assert.DoesNotContain("Dokument potwierdzający", html);
    }
}