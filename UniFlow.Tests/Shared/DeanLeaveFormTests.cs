using System.ComponentModel.DataAnnotations;
using UniFlow.Shared.Forms;

namespace UniFlow.Tests.Shared;

public class DeanLeaveFormTests
{
    private static DeanLeaveForm ValidForm() => new()
    {
        Kind = DeanLeaveKind.Health,
        Period = DeanLeavePeriod.WinterSemester,
        AcademicYear = "2026/2027",
        Justification = "Leczenie szpitalne."
    };

    private static List<string> ErrorFields(DeanLeaveForm form) =>
        form.Validate(new ValidationContext(form)).SelectMany(e => e.MemberNames).ToList();

    [Fact]
    public void Validate_ValidForm_HasNoErrors()
    {
        Assert.Empty(ErrorFields(ValidForm()));
    }

    [Fact]
    public void Validate_EmptyForm_ReportsEachField()
    {
        var form = new DeanLeaveForm();

        Assert.Equal(
            [nameof(form.Kind), nameof(form.Period), nameof(form.AcademicYear), nameof(form.Justification)],
            ErrorFields(form));
    }

    [Theory]
    [InlineData("2026/2027", true)]
    [InlineData("2026/2028", false)]
    [InlineData("2026-2027", false)]
    public void Validate_AcademicYear_TwoYearsOneAfterAnother(string year, bool valid)
    {
        var form = ValidForm();
        form.AcademicYear = year;

        Assert.Equal(valid, ErrorFields(form).Count == 0);
    }

    [Theory]
    [InlineData("2026/2027", true)]
    [InlineData("2027/2028", true)]
    [InlineData("2025/2026", false)]
    [InlineData("2028/2029", false)]
    public void ValidateAgainst_OnlyCurrentOrNextYear(string year, bool valid)
    {
        var form = ValidForm();
        form.AcademicYear = year;

        Assert.Equal(valid, !form.ValidateAgainst(currentStartYear: 2026).Any());
    }

    [Theory]
    [InlineData(DeanLeaveKind.Health, 1)]
    [InlineData(DeanLeaveKind.Parental, 1)]
    [InlineData(DeanLeaveKind.Occasional, 0)]
    public void RequiredAttachments_DependOnKind(DeanLeaveKind kind, int expected)
    {
        Assert.Equal(expected, new DeanLeaveForm { Kind = kind }.RequiredAttachments);
    }
}