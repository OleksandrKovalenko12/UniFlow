using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using UniFlow.Shared.Forms;

namespace UniFlow.Tests.Shared;

public class GroupChangeFormTests
{
    private static GroupChangeForm GeneralForm() => new()
    {
        Scope = GroupChangeScope.General,
        GroupType = GroupType.Laboratory,
        CurrentGroup = "3",
        RequestedGroup = "4",
        Justification = "Kolizja z pracą."
    };

    private static SubjectGroupChange Row(string subject = "Bazy danych",
        GroupType groupType = GroupType.Laboratory, string current = "L2", string requested = "L4") =>
        new() { Subject = subject, GroupType = groupType, CurrentGroup = current, RequestedGroup = requested };

    private static GroupChangeForm SubjectsForm(params SubjectGroupChange[] rows) => new()
    {
        Scope = GroupChangeScope.Subjects,
        Subjects = [.. rows],
        Justification = "Studiuję na dwóch kierunkach."
    };

    // The same call that Api makes before saving
    private static List<ValidationResult> Validate(GroupChangeForm form)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(form, new ValidationContext(form), results, validateAllProperties: true);
        return results;
    }

    private static void AssertSingleError(GroupChangeForm form, string memberName)
    {
        var error = Assert.Single(Validate(form));
        Assert.Equal([memberName], error.MemberNames);
    }

    //Valid forms

    [Fact]
    public void Validate_ValidGeneralForm_NoErrors()
    {
        Assert.Empty(Validate(GeneralForm()));
    }

    [Fact]
    public void Validate_ValidSubjectsForm_NoErrors()
    {
        var form = SubjectsForm(Row(), Row("Sieci komputerowe", GroupType.Exercises, "Ć1", "Ć2"));

        Assert.Empty(Validate(form));
    }

    //Always

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public void Validate_UnknownScope_Error(int scope)
    {
        var form = GeneralForm();
        form.Scope = (GroupChangeScope)scope;

        AssertSingleError(form, nameof(GroupChangeForm.Scope));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyJustification_Error(string justification)
    {
        var form = GeneralForm();
        form.Justification = justification;

        AssertSingleError(form, nameof(GroupChangeForm.Justification));
    }

    [Fact]
    public void Validate_JustificationAtLimit_NoErrors()
    {
        var form = GeneralForm();
        form.Justification = new string('a', GroupChangeForm.MaxJustificationLength);

        Assert.Empty(Validate(form));
    }

    [Fact]
    public void Validate_JustificationTooLong_Error()
    {
        var form = GeneralForm();
        form.Justification = new string('a', GroupChangeForm.MaxJustificationLength + 1);

        AssertSingleError(form, nameof(GroupChangeForm.Justification));
    }

    //General scope

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public void Validate_General_UnknownGroupType_Error(int groupType)
    {
        var form = GeneralForm();
        form.GroupType = (GroupType)groupType;

        AssertSingleError(form, nameof(GroupChangeForm.GroupType));
    }

    [Fact]
    public void Validate_General_DeanGroup_NoErrors()
    {
        var form = GeneralForm();
        form.GroupType = GroupType.Dean;

        Assert.Empty(Validate(form));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Validate_General_MissingCurrentGroup_Error(string? group)
    {
        var form = GeneralForm();
        form.CurrentGroup = group;

        AssertSingleError(form, nameof(GroupChangeForm.CurrentGroup));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Validate_General_MissingRequestedGroup_Error(string? group)
    {
        var form = GeneralForm();
        form.RequestedGroup = group;

        AssertSingleError(form, nameof(GroupChangeForm.RequestedGroup));
    }

    [Fact]
    public void Validate_General_GroupTooLong_Error()
    {
        var form = GeneralForm();
        form.CurrentGroup = new string('1', GroupChangeForm.MaxGroupLength + 1);

        AssertSingleError(form, nameof(GroupChangeForm.CurrentGroup));
    }

    [Theory]
    [InlineData("3", "3")]
    [InlineData("L2", "l2")]
    [InlineData("3", " 3 ")]
    public void Validate_General_SameGroups_Error(string current, string requested)
    {
        var form = GeneralForm();
        form.CurrentGroup = current;
        form.RequestedGroup = requested;

        AssertSingleError(form, nameof(GroupChangeForm.RequestedGroup));
    }

    //Subjects scope

    [Fact]
    public void Validate_Subjects_EmptyList_Error()
    {
        AssertSingleError(SubjectsForm(), nameof(GroupChangeForm.Subjects));
    }

    [Fact]
    public void Validate_Subjects_TooManyRows_Error()
    {
        var rows = Enumerable.Range(1, GroupChangeForm.MaxSubjects + 1)
            .Select(i => Row($"Przedmiot {i}"))
            .ToArray();

        AssertSingleError(SubjectsForm(rows), nameof(GroupChangeForm.Subjects));
    }

    [Fact]
    public void Validate_Subjects_MaxRows_NoErrors()
    {
        var rows = Enumerable.Range(1, GroupChangeForm.MaxSubjects)
            .Select(i => Row($"Przedmiot {i}"))
            .ToArray();

        Assert.Empty(Validate(SubjectsForm(rows)));
    }

    [Fact]
    public void Validate_Subjects_MissingSubjectInSecondRow_ErrorPointsToRow()
    {
        var form = SubjectsForm(Row(), Row(subject: ""));

        AssertSingleError(form, "Subjects[1].Subject");
    }

    [Fact]
    public void Validate_Subjects_SubjectTooLong_Error()
    {
        var form = SubjectsForm(Row(subject: new string('a', GroupChangeForm.MaxSubjectLength + 1)));

        AssertSingleError(form, "Subjects[0].Subject");
    }

    [Fact]
    public void Validate_Subjects_DeanGroup_Error()
    {
        var form = SubjectsForm(Row(groupType: GroupType.Dean));

        AssertSingleError(form, "Subjects[0].GroupType");
    }

    [Fact]
    public void Validate_Subjects_UnknownGroupType_Error()
    {
        var form = SubjectsForm(Row(groupType: 0));

        AssertSingleError(form, "Subjects[0].GroupType");
    }

    [Fact]
    public void Validate_Subjects_MissingGroups_ErrorForEachGroup()
    {
        var form = SubjectsForm(Row(current: "", requested: ""));

        var errors = Validate(form);

        Assert.Equal(2, errors.Count);
        Assert.Contains(errors, e => e.MemberNames.Single() == "Subjects[0].CurrentGroup");
        Assert.Contains(errors, e => e.MemberNames.Single() == "Subjects[0].RequestedGroup");
    }

    [Fact]
    public void Validate_Subjects_SameGroups_Error()
    {
        var form = SubjectsForm(Row(current: "L2", requested: "L2"));

        AssertSingleError(form, "Subjects[0].RequestedGroup");
    }

    [Fact]
    public void Validate_Subjects_DuplicateRow_ErrorOnSecondRow()
    {
        var form = SubjectsForm(Row("Bazy danych"), Row(" bazy danych "));

        AssertSingleError(form, "Subjects[1].Subject");
    }

    [Fact]
    public void Validate_Subjects_SameSubjectDifferentGroupType_NoErrors()
    {
        var form = SubjectsForm(
            Row("Bazy danych", GroupType.Laboratory),
            Row("Bazy danych", GroupType.Exercises, "Ć1", "Ć2"));

        Assert.Empty(Validate(form));
    }

    [Fact]
    public void Validate_Subjects_NullRow_Error()
    {
        var form = SubjectsForm(Row(), null!);

        AssertSingleError(form, nameof(GroupChangeForm.Subjects));
    }

    //Normalize

    [Fact]
    public void Normalize_General_TrimsAndClearsSubjects()
    {
        var form = GeneralForm();
        form.CurrentGroup = " 3 ";
        form.RequestedGroup = "4\n";
        form.Subjects = [Row()];
        form.Justification = "  Kolizja z pracą.  ";

        form.Normalize();

        Assert.Equal("3", form.CurrentGroup);
        Assert.Equal("4", form.RequestedGroup);
        Assert.Empty(form.Subjects);
        Assert.Equal("Kolizja z pracą.", form.Justification);
    }

    [Fact]
    public void Normalize_Subjects_TrimsRowsAndClearsGeneralFields()
    {
        var form = SubjectsForm(Row(" Bazy danych ", GroupType.Laboratory, " L2", "L4 "));
        form.GroupType = GroupType.Dean;
        form.CurrentGroup = "3";
        form.RequestedGroup = "4";

        form.Normalize();

        Assert.Equal(default, form.GroupType);
        Assert.Null(form.CurrentGroup);
        Assert.Null(form.RequestedGroup);
        var row = Assert.Single(form.Subjects);
        Assert.Equal("Bazy danych", row.Subject);
        Assert.Equal("L2", row.CurrentGroup);
        Assert.Equal("L4", row.RequestedGroup);
    }

    [Fact]
    public void Normalize_ThenValidate_NoErrors()
    {
        var form = SubjectsForm(Row(" Bazy danych "));
        form.CurrentGroup = "stale";

        form.Normalize();

        Assert.Empty(Validate(form));
    }

    //Blazor EditForm (Client)

    // Blazor keeps a row error under the form and the path "Subjects[1].Subject",
    // not under the row object - the Client reads it by this path
    [Fact]
    public void EditContext_RowError_IsStoredUnderFormAndPath()
    {
        var row = Row(subject: "");
        var form = SubjectsForm(Row(), row);
        var editContext = new EditContext(form);
        editContext.EnableDataAnnotationsValidation(new ServiceCollection().BuildServiceProvider());

        var isValid = editContext.Validate();

        Assert.False(isValid);
        Assert.Equal(["Podaj nazwę przedmiotu."],
            editContext.GetValidationMessages(new FieldIdentifier(form, "Subjects[1].Subject")));
        Assert.Empty(editContext.GetValidationMessages(new FieldIdentifier(row, nameof(row.Subject))));
    }
}