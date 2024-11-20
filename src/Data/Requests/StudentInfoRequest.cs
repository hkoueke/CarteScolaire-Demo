namespace Data.Requests;

/// <summary>
/// A class representing query string parameters for a student search.
/// It is used to query the https://cartescolaire.cm API for a specific student.
/// </summary>
public sealed class StudentInfoRequest
{
    public string SchoolId { get; init; }
    public string Name { get; init; }

    /// <param name="schoolId">The unique identifier of the student's school.</param>
    /// <param name="name">The name of the student. It can be either full or partial.</param>
    public StudentInfoRequest(string schoolId, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schoolId, nameof(schoolId));
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));

        SchoolId = schoolId.ToUpper();
        Name = name.ToUpper();
    }
}
