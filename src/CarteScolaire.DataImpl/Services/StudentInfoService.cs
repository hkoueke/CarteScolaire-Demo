using System.Collections.Specialized;
using System.Web;
using CarteScolaire.Data.Queries;
using CarteScolaire.Data.Responses;
using CarteScolaire.Data.Services;

namespace CarteScolaire.DataImpl.Services;

internal sealed class StudentInfoService(
    HttpClient httpClient, 
    IHtmlParser<StudentInfoResponse> htmlParser) : IStudentInfoService
{
    public async Task<Result<IReadOnlyCollection<StudentInfoResponse>>> GetStudentInfoAsync(
        StudentInfoQuery query,
        CancellationToken cancellationToken = default)
    {
        if (httpClient.BaseAddress is null)
        {
            return Result<IReadOnlyCollection<StudentInfoResponse>>.Failure("BaseAddress is not set on the HttpClient.");
        }

        try
        {
            NameValueCollection parts = HttpUtility.ParseQueryString(string.Empty);
            parts["student_name"] = query.Name;
            parts["school_code"] = query.SchoolId;

            var uriBuilder = new UriBuilder(httpClient.BaseAddress)
            {
                Query = parts.ToString()
            };

            using HttpResponseMessage response = await httpClient.GetAsync(
                uriBuilder.Uri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var httpError = $"Failed to retrieve data. Status code: {(int)response.StatusCode} {response.StatusCode} [{response.ReasonPhrase}]";
                return Result<IReadOnlyCollection<StudentInfoResponse>>.Failure(httpError);
            }

            using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            return await htmlParser.ParseAsync(stream, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyCollection<StudentInfoResponse>>.Failure($"An unexpected error occurred: {ex.Message}");
        }
    }
}
