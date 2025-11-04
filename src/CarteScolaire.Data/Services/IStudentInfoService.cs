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
    /// A task representing the asynchronous operation. The task result contains a read-only collection of <see cref="StudentInfoResponse"/> 
    /// matching the provided criteria.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="query"/> is <c>null</c>.</exception>
    /// <exception cref="HttpRequestException">Thrown if the HTTP request to retrieve data fails.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the response cannot be parsed or processed correctly.</exception>
    /// <exception cref="TaskCanceledException">Thrown if the operation is canceled via <paramref name="cancellationToken"/>.</exception>
    public Task<IReadOnlyCollection<StudentInfoResponse>> GetStudentInfoAsync(StudentInfoQuery query, CancellationToken cancellationToken = default);
}
