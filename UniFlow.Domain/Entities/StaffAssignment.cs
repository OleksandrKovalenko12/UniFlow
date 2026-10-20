namespace UniFlow.Domain.Entities;

public class StaffAssignment
{
    public int Id { get; private set; }
    public string UserId { get; private set; } = null!;
    public int FacultyId { get; private set; }
    public Faculty Faculty { get; private set; } = null!;

    public StaffAssignment(string userId, Faculty faculty)
    {
        if(string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId is required.", nameof(userId));
        
        ArgumentNullException.ThrowIfNull(faculty);

        Faculty = faculty;
        FacultyId = faculty.Id;
        UserId = userId;
    }

    private StaffAssignment() { }
}
