using System.Text.Json.Serialization;

namespace UniFlow.Shared.Forms;

[JsonConverter(typeof(JsonStringEnumConverter<GroupType>))]
public enum GroupType
{
    Dean = 1,
    Exercises = 2,
    Laboratory = 3,
    Project = 4
}