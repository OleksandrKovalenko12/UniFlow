using System.Text.Json.Serialization;

namespace UniFlow.Shared.Forms;

[JsonConverter(typeof(JsonStringEnumConverter<DeanLeaveKind>))]
public enum DeanLeaveKind
{
    Health = 1,
    Occasional = 2,
    Parental = 3
}