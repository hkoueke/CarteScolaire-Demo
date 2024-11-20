using Data.Requests;
using Data.Responses;

namespace Data.Services;

/// <summary>
/// A service to query https://cartescolaire.cm for student information.
/// </summary>
public interface IStudentInfoService
{
    /// <summary>
    /// Asynchronously retrieves a list of <see cref="StudentInfo"/> based on the given request parameters.
    /// </summary>
    /// <param name="request">The request object containing filters such as school ID and student name.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests, allowing the operation to be cancelled if needed.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains an <see cref="IEnumerable{StudentInfo}"/>
    /// with the student information matching the request criteria.</returns>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="HttpRequestException"/>
    /// <exception cref="InvalidOperationException"/>
    /// <exception cref="TaskCanceledException"/>
    public Task<IEnumerable<StudentInfo>> GetStudentInfoAsync(StudentInfoRequest request, CancellationToken cancellationToken = default);
}
