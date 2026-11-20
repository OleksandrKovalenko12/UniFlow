using System.Text.Json.Serialization;

namespace UniFlow.Shared.Forms;

[JsonConverter(typeof(JsonStringEnumConverter<PersonalDataChangeReason>))]
public enum PersonalDataChangeReason
{
    Marriage = 1,
    Divorce = 2,
    NameChangeDecision = 3,
    Other = 4
}