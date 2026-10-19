namespace UniFlow.Domain.Entities;

public class Faculty
{
    public int Id { get; private set; }
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;

    public Faculty(string code, string name)
    {
        Name = NormalizeName(name);
        Code = NormalizeCode(code);
    }

    private Faculty() { }

    public void Rename(string newName)
    {
        Name = NormalizeName(newName);
    }

    public void ChangeCode(string newCode)
    {
        Code = NormalizeCode(newCode);
    }

    private static string NormalizeCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Code is required.", nameof(code));

        var normalized = code.Trim();

        if (normalized.Length < 2 || normalized.Length > 8)
            throw new ArgumentException("Code must be 2-8 characters long.", nameof(code));
        if (!normalized.All(char.IsLetterOrDigit))
            throw new ArgumentException("Code may contain only letters and digits.", nameof(code));

        return normalized;
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        var normalized = name.Trim();

        if (normalized.Length > 200)
            throw new ArgumentException("Name must be maximum 200 characters long.", nameof(name));

        return normalized;
    }

}
