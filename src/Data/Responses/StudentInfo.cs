namespace Data.Responses;

/// <summary>
/// A class representing a response to a query about a student.
/// </summary>
/// <param name="RegistrationId">The student's unique registration ID (Matricule in French).</param>
/// <param name="Name">The full name of the student.</param>
/// <param name="DateOfBirth">The student's date of birth. This can be null if the date of birth is not available.</param>
/// <param name="Gender">The student's gender.</param>
/// <param name="SchoolName">The name of the student's school.</param>
/// <param name="Class">The class or grade level of the student.</param>
public sealed record StudentInfo(
    string RegistrationId,
    string Name,
    DateOnly? DateOfBirth,
    Gender Gender,
    string SchoolName,
    string Class);
