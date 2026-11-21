using System.Text.Json.Serialization;

namespace UniFlow.Shared.Forms;

[JsonConverter(typeof(JsonStringEnumConverter<DeanLeavePeriod>))]
public enum DeanLeavePeriod
{
    WinterSemester = 1,
    SummerSemester = 2,
    WholeYear = 3
}