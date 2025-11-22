using System.Collections.Specialized;
using System.Net;
using System.Text;
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

        // On récupère un Result<string>, PAS un string directement
        var result = await cache.GetOrSetAsync(
            CacheKey,
            async ct => await tokenProvider.GetTokenAsync(ct),
            options => options
                .SetDuration(TimeSpan.FromHours(2))
                .SetFailSafe(true)
                .SetFactoryTimeouts(TimeSpan.FromSeconds(10))
                .SetEagerRefresh(0.9f),
            cancellationToken);

        // On valide que le token est non vide ET on gère tous les cas d'échec
        return await result
            .Ensure(token => !string.IsNullOrWhiteSpace(token), "Token provider returned a null or empty token")
            .Match(
                onSuccess: token => AppendTokenAndSendAsync(request, token, cancellationToken),
                onFailure: error =>
                {
                    logger.LogWarning("Failed to obtain CSRF token: {Error}", error );
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

    private static HttpResponseMessage CreateTokenFailureResponse(string? message)
    {
        var text = message ?? "An unknown error occurred while retrieving the CSRF token.";
        return new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            ReasonPhrase = text,
            Content = new StringContent(text, Encoding.UTF8, "text/plain")
        };
    }
}
