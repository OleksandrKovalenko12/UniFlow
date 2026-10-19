using UniFlow.Domain.Enums;

namespace UniFlow.Domain.Entities;

public class FieldOfStudy
{
    public int Id { get; private set; }
    public int FacultyId { get; private set; }
    public string Name { get; private set; } = null!;
    public StudyLevel Level { get; private set; }
    public Faculty Faculty { get; private set; } = null!;

    public FieldOfStudy(string name, StudyLevel level, Faculty faculty)
    {
        ArgumentNullException.ThrowIfNull(faculty);

        if (!Enum.IsDefined(level))
            throw new ArgumentOutOfRangeException(nameof(level), level, "Unknown study level.");

        Name = NormalizeName(name);
        Level = level;
        Faculty = faculty;
        FacultyId = faculty.Id;
    }

    private FieldOfStudy() { }

    public void Rename(string newName)
    {
        Name = NormalizeName(newName);
    }

    public void MoveToFaculty(Faculty newFaculty)
    {
        ArgumentNullException.ThrowIfNull(newFaculty);

        Faculty = newFaculty;
        FacultyId = newFaculty.Id;
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        var normalized = name.Trim();

        if (normalized.Length > 200)
            throw new ArgumentException("Name must be at most 200 characters long.", nameof(name));

        return normalized;
    }
}

