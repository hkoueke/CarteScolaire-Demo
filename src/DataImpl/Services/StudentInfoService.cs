using Data.Requests;
using Data.Responses;
using Data.Services;
using System.Collections.Specialized;
using System.Web;

namespace DataImpl.Services;

internal sealed class StudentInfoService(HttpClient httpClient, IHtmlParser<StudentInfo> htmlParser) : IStudentInfoService
{
    public async Task<IEnumerable<StudentInfo>> GetStudentInfoAsync(
        StudentInfoRequest request,
        CancellationToken cancellationToken = default)
    {
        var uriBuilder = new UriBuilder(httpClient.BaseAddress ??
            throw new InvalidOperationException("BaseAddress is not set on the HttpClient. Ensure that the BaseAddress property is initialized before creating the UriBuilder."));

        NameValueCollection query = HttpUtility.ParseQueryString(uriBuilder.Query);
        query["student_name"] ??= request.Name;
        query["school_code"] ??= request.SchoolId;
        uriBuilder.Query = query.ToString();

        HttpResponseMessage response =
            await httpClient.GetAsync(uriBuilder.Uri, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode) return [];

        var htmlContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return await htmlParser.ParseAsync(htmlContent, cancellationToken).ConfigureAwait(false);
    }
}
