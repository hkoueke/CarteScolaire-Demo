using System.Collections.Specialized;
using System.Web;
using CarteScolaire.Data.Services;
using CarteScolaire.DataImpl.Helpers;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace CarteScolaire.DataImpl.Handlers;

internal sealed class TokenAppendingHandler(
    ITokenProvider<string> tokenProvider,
    IFusionCache cache,
    ILogger<TokenAppendingHandler> logger) : DelegatingHandler
{
    private const string CacheKey = "csrf-token";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri is null)
        {
            logger.LogWarning("RequestUri is null. Ensure 'HttpClient.BaseAddress' is configured before sending requests.");
            return CreateTokenFailureResponse("Invalid request: missing Request Uri.");
        }

        var result = await cache.GetOrSetAsync(
            CacheKey,
            async _ => await tokenProvider.GetTokenAsync(cancellationToken).ConfigureAwait(false),
            options => options
                .SetDuration(TimeSpan.FromHours(2))
                .SetFailSafe(true)
                .SetFactoryTimeouts(TimeSpan.FromSeconds(10))
                .SetEagerRefresh(0.9f),
            cancellationToken
        );

        return await result
            .Ensure(token => !string.IsNullOrWhiteSpace(token), "Token provider returned a null or empty token")
            .Match(
                onSuccess: token => AppendTokenAndSendAsync(request, token, cancellationToken),
                onFailure: error =>
                {
                    logger.LogWarning("An unexpected error occured: {Error}.", error);
                    return Task.FromResult(CreateTokenFailureResponse(error));
                }
            );
    }

    private Task<HttpResponseMessage> AppendTokenAndSendAsync(HttpRequestMessage request, string token, CancellationToken cancellationToken)
    {
        var uriBuilder = new UriBuilder(request.RequestUri!);
        NameValueCollection parts = HttpUtility.ParseQueryString(uriBuilder.Query);

        parts["_token"] = token;
        uriBuilder.Query = parts.ToString();
        request.RequestUri = uriBuilder.Uri;

        // Proceed with the modified request
        return base.SendAsync(request, cancellationToken);
    }

    private static HttpResponseMessage CreateTokenFailureResponse(string message) => new(System.Net.HttpStatusCode.InternalServerError)
    {
        ReasonPhrase = message,
        Content = new StringContent(message, System.Text.Encoding.UTF8, "text/plain")
    };
}
