namespace CarteScolaire.Data.Queries;

/// <summary>
/// A class representing query string parameters for a student search.
/// It is used to query the https://cartescolaire.cm API for a specific student.
/// </summary>
public sealed class StudentInfoQuery
{
    public string SchoolId { get; }
    public string Name { get; }

    /// <param name="schoolId">The unique identifier of the student's school.</param>
    /// <param name="name">The name of the student. It can be either full or partial.</param>
    public StudentInfoQuery(string schoolId, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schoolId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        SchoolId = schoolId.ToUpper();
        Name = name.ToUpper();
    }
}
