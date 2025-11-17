using CarteScolaire.Data.Queries;
using CarteScolaire.Data.Responses;

namespace CarteScolaire.Data.Services;

/// <summary>
/// A service to query https://cartescolaire.cm for student information.
/// </summary>
public interface IStudentInfoService
{
    /// <summary>
    /// Asynchronously retrieves a collection of <see cref="StudentInfoResponse"/> objects based on the specified query parameters.
    /// </summary>
    /// <param name="query">The query containing filters such as school ID and student name. Must not be <c>null</c>.</param>
    /// <param name="cancellationToken">Token to observe for cancellation requests. Optional.</param>
    /// <returns>
    /// A task representing the asynchronous operation. The task result contains a <see cref="Result{T}"/>,
    /// which is either a success with a read-only collection of <see cref="StudentInfoResponse"/> 
    /// matching the criteria, or a failure with an error message.
    /// </returns>
    public Task<Result<StudentInfoCollection>> GetStudentInfoAsync(StudentInfoQuery query, CancellationToken cancellationToken = default);
}
