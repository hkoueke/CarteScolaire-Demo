using Data.Services;
using Microsoft.Extensions.Caching.Distributed;
using System.Collections.Specialized;
using System.Web;

namespace DataImpl.Handlers;

internal sealed class TokenAppendingHandler(
    ITokenFetcher<string> _tokenFetcher,
    IDistributedCache _cache) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        //Throw exception if URI is null
        ArgumentNullException.ThrowIfNull(request.RequestUri);

        var cacheEntryOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(2)
        };

        // Append the token as a query string parameter
        var uriBuilder = new UriBuilder(request.RequestUri);
        NameValueCollection query = HttpUtility.ParseQueryString(uriBuilder.Query);

        if (string.IsNullOrWhiteSpace(query["_token"]))
        {
            // Fetch the token dynamically: Cache-aside
            string token = await _cache.GetOrCreateAsync("querystring-token", async () =>
            {
                return await _tokenFetcher
                    .FetchTokenAsync(cancellationToken)
                    .ConfigureAwait(false);
            }, cacheEntryOptions, cancellationToken: cancellationToken).ConfigureAwait(false);

            //Throw exception if token is null, empty or whitespace
            ArgumentException.ThrowIfNullOrWhiteSpace(token);

            query.Add("_token", token);
        }

        uriBuilder.Query = query.ToString();

        if (request.RequestUri != uriBuilder.Uri)
        {
            request.RequestUri = uriBuilder.Uri;
        }

        // Proceed with the request
        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
