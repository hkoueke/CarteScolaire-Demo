using System.Collections.Specialized;
using System.Web;
using CarteScolaire.Data.Services;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace CarteScolaire.DataImpl.Handlers;

internal sealed class TokenAppendingHandler(
    ITokenProvider<string> _tokenProvider,
    IFusionCache cache,
    ILogger<TokenAppendingHandler> logger) : DelegatingHandler
{
    private const string CacheKey = "csrf-token";
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request.RequestUri);

        try
        {
            var token = await cache.GetOrSetAsync(
                CacheKey,
                async _ => await _tokenProvider.GetTokenAsync(cancellationToken).ConfigureAwait(false),
                options => options
                .SetDuration(TimeSpan.FromHours(2))
                .SetFailSafe(true)
                .SetFactoryTimeouts(TimeSpan.FromMilliseconds(30)) //Timeout for TokenProvider
                .SetEagerRefresh(0.9f), cancellationToken
            );

            if (string.IsNullOrWhiteSpace(token))
            {
                logger.LogError("Token provider returned null or empty token");
                return CreateTokenFailureResponse("Token provider returned null or empty token");
            }

            // Append the token as a query string parameter
            var uriBuilder = new UriBuilder(request.RequestUri);
            NameValueCollection parts = HttpUtility.ParseQueryString(uriBuilder.Query);
            parts["_token"] ??= token;

            uriBuilder.Query = parts.ToString();
            request.RequestUri = uriBuilder.Uri;

            // Proceed with the modified request
            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to acquire CSRF token.");
            return CreateTokenFailureResponse($"Failed to acquire CSRF token. Reason: {ex.Message}");
        }
    }

    private static HttpResponseMessage CreateTokenFailureResponse(string message) => new(System.Net.HttpStatusCode.InternalServerError)
    {
        ReasonPhrase = message,
        Content = new StringContent(message, System.Text.Encoding.UTF8, "text/plain")
    };
}
