using System.ComponentModel.DataAnnotations;

namespace UniFlow.Client.Applications;

// Error messages of a form by field path, e.g. "Justification" or "Subjects[0].RequestedGroup".
// Paths are the same in the browser and on the server, so both kinds of errors land in one place.
public sealed class FormErrors
{
    private Dictionary<string, string[]> _errors = [];

    public bool Any => _errors.Count > 0;

    public bool Has(string path) => _errors.ContainsKey(path);

    public string? For(string path) =>
        _errors.TryGetValue(path, out var messages) ? string.Join(" ", messages) : null;

    // Errors sent by the server - the paths are the same, so they show under the same fields
    public void Set(IReadOnlyDictionary<string, string[]> errors) =>
        _errors = new Dictionary<string, string[]>(errors);

    // Runs the form's own rules - the same ones the server runs - and returns true when there are none
    public bool Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);

        _errors = results
            .SelectMany(r => r.MemberNames, (r, path) => (Path: path, Message: r.ErrorMessage ?? ""))
            .GroupBy(e => e.Path)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Message).ToArray());

        return !Any;
    }
}