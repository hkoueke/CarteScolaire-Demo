namespace CarteScolaire.Data.Responses;

/// <summary>
/// A class representing a response to a query about a student.
/// </summary>
public readonly record struct StudentInfoResponse
{
    /// <summary>
    /// The student's unique registration ID (Matricule in French).
    /// </summary>
    public required string RegistrationId { get; init; }

    /// <summary>
    /// The full name of the student.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The student's date of birth. This can be null.
    /// </summary>
    public DateOnly? DateOfBirth { get; init; }

    /// <summary>
    /// The student's gender.
    /// </summary>
    public required Gender Gender { get; init; }

    /// <summary>
    /// The name of the student's school.
    /// </summary>
    public required string SchoolName { get; init; }

    /// <summary>
    /// The class or grade level of the student.
    /// </summary>
    public required string Class { get; init; }
}
