using System.Collections.Specialized;
using System.Web;
using CarteScolaire.Data.Queries;
using CarteScolaire.Data.Responses;
using CarteScolaire.Data.Services;
using CarteScolaire.DataImpl.Helpers;

namespace CarteScolaire.DataImpl.Services;

internal sealed class StudentInfoService(HttpClient httpClient, IHtmlParser<StudentInfoResponse> htmlParser) : IStudentInfoService
{
    public async Task<Result<StudentInfoCollection>> GetStudentInfoAsync(StudentInfoQuery query, CancellationToken cancellationToken = default)
    {
        if (httpClient.BaseAddress is null)
            return Result<StudentInfoCollection>.Failure("Invalid HTTP request: missing Base Address.");

        return await ResultExtensions.TryAsync(
            async () =>
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
                    cancellationToken
                );

                if (!response.IsSuccessStatusCode)
                {
                    return Result<StudentInfoCollection>.Failure(
                        $"Failed to retrieve data : {response.ReasonPhrase}"
                    );
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                return await htmlParser.ParseAsync(stream, cancellationToken).ConfigureAwait(false);
            }
        );
    }
}
