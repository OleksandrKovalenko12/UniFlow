using System.ComponentModel.DataAnnotations;
using UniFlow.Shared.Forms;

namespace UniFlow.Tests.Shared;

public class PersonalDataChangeFormTests
{
    private static readonly CurrentPersonalData Current = new("Anna", "Testowa", "anna@example.com");

    private static PostalAddress Address(string street, string postalCode, string city = "Lipowa Wola",
        string country = "Polska") =>
        new() { Street = street, PostalCode = postalCode, City = city, Country = country };

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
    [InlineData("10115", true)]
    [InlineData("SW1A 1AA", true)]
    [InlineData("0", false)]
    [InlineData("--", false)]
    [InlineData("00-950 Lipowa Wola", false)]
    public void Validate_PostalCode_AcceptsCodesOfManyCountries(string postalCode, bool valid)
    {
        var form = new PersonalDataChangeForm
        {
            ChangeAddress = true,
            OldAddress = Address("ul. Lipowa 7/2", "11-200"),
            NewAddress = Address("ul. Polna 12", postalCode)
        };

        Assert.Equal(valid, ErrorFields(form).Count == 0);
    }

    [Fact]
    public void Validate_EmptyOldAddress_ReportsItsFields()
    {
        var form = new PersonalDataChangeForm
        {
            ChangeAddress = true,
            NewAddress = Address("ul. Polna 12", "11-300")
        };

        Assert.Equal(
            ["OldAddress.Street", "OldAddress.PostalCode", "OldAddress.City", "OldAddress.Country"],
            ErrorFields(form));
    }

    [Fact]
    public void Validate_SameAddressWrittenDifferently_IsNoChange()
    {
        var form = new PersonalDataChangeForm
        {
            ChangeAddress = true,
            OldAddress = Address("ul. Polna 12", "11-300"),
            NewAddress = Address(" UL. POLNA 12 ", "11-300", "lipowa wola", "polska")
        };

        Assert.Equal(["NewAddress.Street"], ErrorFields(form));
    }

    [Theory]
    [InlineData("600 123 456", "+48 600-111-222", true)]
    [InlineData("600 123 456", "600-123-456", false)]
    [InlineData("600 123 456", "12345", false)]
    public void Validate_Phone_NewMustBeAnotherValidNumber(string oldPhone, string newPhone, bool valid)
    {
        var form = new PersonalDataChangeForm { ChangePhone = true, OldPhone = oldPhone, NewPhone = newPhone };

        Assert.Equal(valid, ErrorFields(form).Count == 0);
    }

    [Fact]
    public void Validate_SameEmailInOtherLetters_IsNoChange()
    {
        var form = new PersonalDataChangeForm
        {
            ChangeEmail = true,
            OldEmail = "anna@example.com",
            NewEmail = "ANNA@example.com"
        };

        Assert.Equal([nameof(form.NewEmail)], ErrorFields(form));
    }

    [Fact]
    public void Normalize_ClearsPartsNotTickedAndPhoneSeparators()
    {
        var form = new PersonalDataChangeForm
        {
            ChangePhone = true,
            OldPhone = "600 111 222",
            NewPhone = "+48 600-123 456",
            NewLastName = "Zostaje po odznaczeniu",
            NewAddress = Address("ul. Polna 12", "11-300")
        };

        form.Normalize();

        Assert.Equal("+48600123456", form.NewPhone);
        Assert.Null(form.NewLastName);
        Assert.True(form.NewAddress.IsEmpty);
    }

    [Fact]
    public void ValidateAgainst_SameName_ReportsLastName()
    {
        var form = new PersonalDataChangeForm { ChangeName = true, NewFirstName = "Anna", NewLastName = "Testowa" };

        Assert.Equal([nameof(form.NewLastName)], form.ValidateAgainst(Current).SelectMany(e => e.MemberNames));
    }

    [Fact]
    public void RequiredAttachments_OnlyForNewName()
    {
        Assert.Equal(1, new PersonalDataChangeForm { ChangeName = true, ChangePhone = true }.RequiredAttachments);
        Assert.Equal(0, new PersonalDataChangeForm { ChangeAddress = true, ChangePhone = true }.RequiredAttachments);
    }
}