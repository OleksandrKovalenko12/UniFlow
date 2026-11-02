using UniFlow.Shared.Forms;

namespace UniFlow.Tests.Shared;

public class GroupTypeTextTests
{
    [Theory]
    [InlineData(GroupType.Dean, "dziekańskiej")]
    [InlineData(GroupType.Exercises, "ćwiczeniowej")]
    [InlineData(GroupType.Laboratory, "laboratoryjnej")]
    [InlineData(GroupType.Project, "projektowej")]
    public void Adjective_KnownGroupType_ReturnsPolishAdjective(GroupType groupType, string expected)
    {
        Assert.Equal(expected, GroupTypeText.Adjective(groupType));
    }

    [Theory]
    [InlineData(GroupType.Dean, "grupa dziekańska")]
    [InlineData(GroupType.Exercises, "ćwiczenia")]
    [InlineData(GroupType.Laboratory, "laboratorium")]
    [InlineData(GroupType.Project, "projekt")]
    public void Name_KnownGroupType_ReturnsPolishName(GroupType groupType, string expected)
    {
        Assert.Equal(expected, GroupTypeText.Name(groupType));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public void Adjective_UnknownGroupType_ThrowsArgumentOutOfRangeException(int value)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => GroupTypeText.Adjective((GroupType)value));

        Assert.Equal("groupType", exception.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public void Name_UnknownGroupType_ThrowsArgumentOutOfRangeException(int value)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => GroupTypeText.Name((GroupType)value));

        Assert.Equal("groupType", exception.ParamName);
    }

    [Fact]
    public void AllGroupTypes_HaveAdjectiveAndName()
    {
        foreach (var groupType in Enum.GetValues<GroupType>())
        {
            Assert.False(string.IsNullOrWhiteSpace(GroupTypeText.Adjective(groupType)));
            Assert.False(string.IsNullOrWhiteSpace(GroupTypeText.Name(groupType)));
        }
    }
}