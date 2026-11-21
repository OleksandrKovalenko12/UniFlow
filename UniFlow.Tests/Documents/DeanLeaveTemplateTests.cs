using Microsoft.Extensions.Logging.Abstractions;
using UniFlow.Documents.Models;
using UniFlow.Documents.Rendering;
using UniFlow.Documents.Templates;
using UniFlow.Shared.Forms;

namespace UniFlow.Tests.Documents;

public class DeanLeaveTemplateTests
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

    private static Task<string> RenderAsync(DeanLeaveKind kind, DeanLeavePeriod period) =>
        new DocumentRenderer(NullLoggerFactory.Instance)
            .RenderAsync<DeanLeaveTemplate, DeanLeaveDocument>(new DeanLeaveDocument(Header, new DeanLeaveForm
            {
                Kind = kind,
                Period = period,
                AcademicYear = "2027/2028",
                Justification = "Leczenie szpitalne."
            }));

    [Fact]
    public async Task Render_HealthLeave_AsksForItAndMentionsTheDocument()
    {
        var html = await RenderAsync(DeanLeaveKind.Health, DeanLeavePeriod.WinterSemester);

        Assert.Contains("<title>Podanie o udzielenie urlopu dziekańskiego</title>", html);
        Assert.Contains("<strong>Prodziekan</strong>", html);
        Assert.Contains("urlopu zdrowotnego", html);
        Assert.Contains("na semestr zimowy roku akademickiego 2027/2028", html);
        Assert.Contains("Dokumentację medyczną dołączam do wniosku.", html);
    }

    [Fact]
    public async Task Render_OccasionalLeave_NeedsNoDocumentSentence()
    {
        var html = await RenderAsync(DeanLeaveKind.Occasional, DeanLeavePeriod.WholeYear);

        Assert.Contains("na rok akademicki 2027/2028", html);
        Assert.DoesNotContain("dołączam do wniosku", html);
    }
}