using System.ComponentModel.DataAnnotations;
using UniFlow.Shared.Forms;

namespace UniFlow.Tests.Shared;

public class PersonalDataChangeFormTests
{
    private static readonly CurrentPersonalData Current = new("Anna", "Testowa", "anna@example.com");

    private static List<string> ErrorFields(PersonalDataChangeForm form) =>
        form.Validate(new ValidationContext(form)).SelectMany(e => e.MemberNames).ToList();

    [Fact]
    public void Validate_NothingTicked_ReportsChanges()
    {
        Assert.Equal([PersonalDataChangeForm.ChangesField], ErrorFields(new PersonalDataChangeForm()));
    }

    [Fact]
    public void Validate_EmptyNamePart_ReportsEachField()
    {
        var form = new PersonalDataChangeForm { ChangeName = true, NewFirstName = " " };

        Assert.Equal(
            [nameof(form.NewFirstName), nameof(form.NewLastName), nameof(form.Reason)],
            ErrorFields(form));
    }

    [Theory]
    [InlineData("00-950", true)]
    [InlineData("00950", false)]
    [InlineData("0-0950", false)]
    public void Validate_PostalCode_MustLookLikePolishOne(string postalCode, bool valid)
    {
        var form = new PersonalDataChangeForm
        {
            ChangeAddress = true,
            Street = "ul. Kwiatowa 5/12",
            PostalCode = postalCode,
            City = "Warszawa"
        };

        Assert.Equal(valid, ErrorFields(form).Count == 0);
    }

    [Theory]
    [InlineData("600 123 456", true)]
    [InlineData("+48 600-123-456", true)]
    [InlineData("12345", false)]
    [InlineData("600 abc 456", false)]
    public void Validate_Phone_AcceptsWhatPeopleType(string phone, bool valid)
    {
        var form = new PersonalDataChangeForm { ChangePhone = true, Phone = phone };

        Assert.Equal(valid, ErrorFields(form).Count == 0);
    }

    [Fact]
    public void Normalize_ClearsPartsNotTickedAndPhoneSeparators()
    {
        var form = new PersonalDataChangeForm
        {
            ChangePhone = true,
            Phone = "+48 600-123 456",
            NewLastName = "Zostaje po odznaczeniu",
            City = "Warszawa"
        };

        form.Normalize();

        Assert.Equal("+48600123456", form.Phone);
        Assert.Null(form.NewLastName);
        Assert.Null(form.City);
    }

    [Fact]
    public void ValidateAgainst_SameNameAndEmail_ReportsBoth()
    {
        var form = new PersonalDataChangeForm
        {
            ChangeName = true,
            NewFirstName = "Anna",
            NewLastName = "Testowa",
            ChangeEmail = true,
            Email = "ANNA@example.com"
        };

        Assert.Equal(
            [nameof(form.NewLastName), nameof(form.Email)],
            form.ValidateAgainst(Current).SelectMany(e => e.MemberNames));
    }

    [Fact]
    public void RequiredAttachments_OnlyForNewName()
    {
        Assert.Equal(1, new PersonalDataChangeForm { ChangeName = true, ChangePhone = true }.RequiredAttachments);
        Assert.Equal(0, new PersonalDataChangeForm { ChangeAddress = true, ChangePhone = true }.RequiredAttachments);
    }
}