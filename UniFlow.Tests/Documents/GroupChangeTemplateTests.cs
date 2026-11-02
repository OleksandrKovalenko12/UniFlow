using Microsoft.Extensions.Logging.Abstractions;
using UniFlow.Documents.Models;
using UniFlow.Documents.Rendering;
using UniFlow.Documents.Templates;
using UniFlow.Shared.Forms;

namespace UniFlow.Tests.Documents;

public class GroupChangeTemplateTests
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

    private static GroupChangeForm GeneralForm(string justification = "Kolizja z pracą.") => new()
    {
        Scope = GroupChangeScope.General,
        GroupType = GroupType.Laboratory,
        CurrentGroup = "3",
        RequestedGroup = "4",
        Justification = justification
    };

    private static GroupChangeForm SubjectsForm(params SubjectGroupChange[] subjects) => new()
    {
        Scope = GroupChangeScope.Subjects,
        Subjects = [.. subjects],
        Justification = "Studiuję na dwóch kierunkach."
    };

    private static Task<string> RenderAsync(GroupChangeForm form) =>
        new DocumentRenderer(NullLoggerFactory.Instance)
            .RenderAsync<GroupChangeTemplate, GroupChangeDocument>(new GroupChangeDocument(Header, form));

    [Fact]
    public async Task Render_Always_ProducesCompleteHtmlDocument()
    {
        var html = await RenderAsync(GeneralForm());

        Assert.StartsWith("<!DOCTYPE html>", html);
        Assert.Contains("<title>Podanie o zmianę grupy</title>", html);
        Assert.EndsWith("</html>", html);
    }

    [Fact]
    public async Task Render_Always_ContainsHeaderData()
    {
        var html = await RenderAsync(GeneralForm());

        Assert.Contains("dnia 02.11.2026 r.", html);
        Assert.Contains("<td>Anna Testowa</td>", html);
        Assert.Contains("<td>100001</td>", html);
        Assert.Contains("<td>Wydział Informatyki</td>", html);
        Assert.Contains("<td>Informatyka</td>", html);
        Assert.Contains("<td>studia stacjonarne, I stopnia</td>", html);
        Assert.Contains("<td>7</td>", html);
        Assert.Contains("<td>2026/2027</td>", html);
    }

    [Fact]
    public async Task Render_Always_ContainsAddresseeTitleAndSignature()
    {
        var html = await RenderAsync(GeneralForm());

        Assert.Contains("<strong>Prodziekan</strong>", html);
        Assert.Contains("""<h1 class="document-title">Podanie o zmianę grupy</h1>""", html);
        Assert.Contains("""<p class="signature">Anna Testowa</p>""", html);
    }

    [Fact]
    public async Task Render_Always_ContainsSubmissionFooter()
    {
        var html = await RenderAsync(GeneralForm());

        Assert.Contains("""<footer class="submission-info">""", html);
        Assert.Contains("Anna Testowa (nr albumu 100001)", html);
        Assert.Contains("02.11.2026 09:41.", html);
        Assert.Contains("Nr wniosku: UF/2026/0001.", html);
    }

    [Fact]
    public async Task Render_GeneralScope_ContainsGroupTypeAndGroupsWithoutTable()
    {
        var html = await RenderAsync(GeneralForm());

        Assert.Contains("o zmianę grupy laboratoryjnej", html);
        Assert.Contains("z grupy nr 3 na grupę nr 4", html);
        Assert.Contains("ze wszystkich przedmiotów", html);
        Assert.DoesNotContain("""<table class="subjects">""", html);
    }

    [Fact]
    public async Task Render_SubjectsScope_ContainsRowForEachSubject()
    {
        var html = await RenderAsync(SubjectsForm(
            new SubjectGroupChange { Subject = "Bazy danych", GroupType = GroupType.Laboratory, CurrentGroup = "L2", RequestedGroup = "L4" },
            new SubjectGroupChange { Subject = "Sieci komputerowe", GroupType = GroupType.Exercises, CurrentGroup = "Ć1", RequestedGroup = "Ć2" }));

        Assert.Contains("""<table class="subjects">""", html);
        Assert.Contains("<td>Bazy danych</td>", html);
        Assert.Contains("<td>laboratorium</td>", html);
        Assert.Contains("<td>L2</td>", html);
        Assert.Contains("<td>L4</td>", html);
        Assert.Contains("<td>Sieci komputerowe</td>", html);
        Assert.Contains("<td>ćwiczenia</td>", html);
        Assert.Contains("<td>Ć1</td>", html);
        Assert.Contains("<td>Ć2</td>", html);
        Assert.DoesNotContain("ze wszystkich przedmiotów", html);
    }

    [Fact]
    public async Task Render_HtmlInJustification_IsEscaped()
    {
        var html = await RenderAsync(GeneralForm("<script>alert(1)</script>"));

        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html);
        Assert.DoesNotContain("<script>", html);
    }

    [Fact]
    public async Task Render_HtmlInSubjectName_IsEscaped()
    {
        var html = await RenderAsync(SubjectsForm(
            new SubjectGroupChange { Subject = "<img src=x>", GroupType = GroupType.Project, CurrentGroup = "P1", RequestedGroup = "P2" }));

        Assert.Contains("<td>&lt;img src=x&gt;</td>", html);
        Assert.DoesNotContain("<img", html);
    }

    [Fact]
    public async Task Render_MultilineJustification_KeepsLineBreak()
    {
        var html = await RenderAsync(GeneralForm("Pierwsza linia.\nDruga linia."));

        // Encoded newline; the browser turns it back into a line break (white-space: pre-line)
        Assert.Contains("""<p class="justification">Pierwsza linia.&#xA;Druga linia.</p>""", html);
    }
}