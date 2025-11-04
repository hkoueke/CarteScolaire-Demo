using System.Collections.Specialized;
using System.Web;
using CarteScolaire.Data.Queries;
using CarteScolaire.Data.Responses;
using CarteScolaire.Data.Services;

namespace CarteScolaire.DataImpl.Services;

internal sealed class StudentInfoService(HttpClient httpClient, IHtmlParser<StudentInfoResponse> htmlParser) : IStudentInfoService
{
    public async Task<IReadOnlyCollection<StudentInfoResponse>> GetStudentInfoAsync(
        StudentInfoQuery query, 
        CancellationToken cancellationToken = default)
    {
        var uriBuilder = new UriBuilder(httpClient.BaseAddress ??
            throw new InvalidOperationException("BaseAddress is not set on the HttpClient."));

        NameValueCollection parts = HttpUtility.ParseQueryString(uriBuilder.Query);
        parts["student_name"] ??= query.Name;
        parts["school_code"] ??= query.SchoolId;

        uriBuilder.Query = parts.ToString();

        using HttpResponseMessage response =
            await httpClient.GetAsync(uriBuilder.Uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return [];

        using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await htmlParser.ParseAsync(stream, cancellationToken).ConfigureAwait(false);
    }
}
