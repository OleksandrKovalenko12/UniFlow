using System.Text.Json.Serialization;


namespace UniFlow.Shared.Forms;

[JsonConverter(typeof(JsonStringEnumConverter<GroupChangeScope>))]
public enum GroupChangeScope
{
    General = 1,
    Subjects = 2
}