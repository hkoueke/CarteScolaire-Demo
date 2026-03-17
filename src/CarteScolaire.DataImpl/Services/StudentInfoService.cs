using System.Collections.Specialized;
using System.Web;
using CarteScolaire.Data.Queries;
using CarteScolaire.Data.Responses;
using CarteScolaire.Data.Services;
using CarteScolaire.DataImpl.FuzzySearch;

namespace CarteScolaire.DataImpl.Services;

#pragma warning disable CA2007
#pragma warning disable CA1031
#pragma warning disable CA1859

internal sealed class StudentInfoService(HttpClient httpClient, IHtmlParser<StudentInfoResponse> htmlParser) : IStudentInfoService
{
    public async Task<Result<StudentInfoCollection>> GetStudentInfoAsync(SearchQuery query, CancellationToken cancellationToken = default)
    {
        if (httpClient.BaseAddress is null)
        {
            return Result<StudentInfoCollection>.Failure("Invalid HTTP request: BaseAddress is not set on the HttpClient.");
        }

        try
        {
            NameValueCollection parts = HttpUtility.ParseQueryString(string.Empty);
            parts["student_name"] ??= query.Name;
            parts["school_code"] ??= query.SchoolId;
            UriBuilder uriBuilder = new(httpClient.BaseAddress)
            {
                Query = parts.ToString() ?? string.Empty
            };

            using HttpResponseMessage response = await httpClient
                .GetAsync(uriBuilder.Uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return Result<StudentInfoCollection>.Failure($"Failed to retrieve data: {response.ReasonPhrase}");
            }

            await using Stream stream = await response.Content
                .ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);

            Result<StudentInfoCollection> parseResult = await htmlParser
                .ParseAsync(stream, cancellationToken)
                .ConfigureAwait(false);

            if (parseResult.IsFailure)
            {
                return Result<StudentInfoCollection>.Failure("No student information found for the given query parameters.");
            }

            StudentInfoCollection results = PerformFuzzySearch(parseResult.Value, query);
            
            return results.Count > 0
                ? Result.Success(results)
                : Result<StudentInfoCollection>.Failure("No student information matched the search criteria.");
        }
        catch (Exception ex)
        {
            return Result<StudentInfoCollection>.Failure(ex.Message);
        }
    }

    private static StudentInfoCollection PerformFuzzySearch(in StudentInfoCollection items, SearchQuery query)
    {
        using FuzzySearchService<StudentInfoResponse> service = new();
        service.Build(items);

        return service
            .Search(query)
            .OrderByDescending(x => x.Score)
            .Select(x => x.Item)
            .ToList();
    }
}