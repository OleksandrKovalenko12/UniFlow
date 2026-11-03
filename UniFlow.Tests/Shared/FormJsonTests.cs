using System.Text.Json;
using UniFlow.Shared.Forms;

namespace UniFlow.Tests.Shared;

public class FormJsonTests
{
    private static GroupChangeForm SubjectsForm() => new()
    {
        Scope = GroupChangeScope.Subjects,
        Subjects =
        [
            new SubjectGroupChange { Subject = "Bazy danych", GroupType = GroupType.Laboratory, CurrentGroup = "L2", RequestedGroup = "L4" }
        ],
        Justification = "Zażółć gęślą jaźń."
    };

    [Fact]
    public void SerializeThenDeserialize_ReturnsEquivalentForm()
    {
        var form = SubjectsForm();

        var result = FormJson.Deserialize<GroupChangeForm>(FormJson.Serialize(form));

        Assert.Equivalent(form, result, strict: true);
    }

    [Fact]
    public void Serialize_WritesEnumsAsTextAndNamesInCamelCase()
    {
        var json = FormJson.Serialize(SubjectsForm());

        Assert.Contains("\"scope\":\"Subjects\"", json);
        Assert.Contains("\"groupType\":\"Laboratory\"", json);
        Assert.Contains("\"currentGroup\":\"L2\"", json);
    }

    [Fact]
    public void Deserialize_EnumAsText_ReadsValue()
    {
        var form = FormJson.Deserialize<GroupChangeForm>(
            """{"scope":"General","groupType":"Dean","currentGroup":"1","requestedGroup":"2","justification":"x"}""");

        Assert.Equal(GroupChangeScope.General, form.Scope);
        Assert.Equal(GroupType.Dean, form.GroupType);
    }

    [Fact]
    public void Deserialize_UnknownEnumText_Throws()
    {
        Assert.Throws<JsonException>(() => FormJson.Deserialize<GroupChangeForm>(
            """{"scope":"Everything","justification":"x"}"""));
    }

    [Fact]
    public void Deserialize_NullForNonNullableProperty_Throws()
    {
        Assert.Throws<JsonException>(() => FormJson.Deserialize<GroupChangeForm>(
            """{"scope":"General","justification":null}"""));
    }

    [Fact]
    public void Deserialize_JsonNull_Throws()
    {
        Assert.Throws<JsonException>(() => FormJson.Deserialize<GroupChangeForm>("null"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Deserialize_EmptyText_Throws(string json)
    {
        var ex = Assert.Throws<ArgumentException>(() => FormJson.Deserialize<GroupChangeForm>(json));

        Assert.Equal("json", ex.ParamName);
    }

    [Fact]
    public void Serialize_Null_Throws()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => FormJson.Serialize<GroupChangeForm>(null!));

        Assert.Equal("form", ex.ParamName);
    }
}