using System.Text.Json;
using System.Text.Json.Serialization;

namespace UniFlow.Shared.Forms;

// One place for the JSON format of Application.FormData (jsonb)
public static class FormJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        // "Laboratory" instead of 3 - readable in the database
        Converters = { new JsonStringEnumConverter() },
        // null for a non-nullable property (e.g. "justification": null) is an error
        RespectNullableAnnotations = true
    };

    public static string Serialize<TForm>(TForm form) where TForm : class
    {
        ArgumentNullException.ThrowIfNull(form);
        return JsonSerializer.Serialize(form, Options);
    }

    public static TForm Deserialize<TForm>(string json) where TForm : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        return JsonSerializer.Deserialize<TForm>(json, Options)
            ?? throw new JsonException("Form data is null.");
    }
}