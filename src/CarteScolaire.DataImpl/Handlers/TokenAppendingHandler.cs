using System.Collections.Specialized;
using System.Net;
using System.Text;
using System.Web;
using CarteScolaire.Data.Services;
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
            logger.LogWarning("RequestUri is null. Ensure 'BaseAddress' is configured on the HttpClient.");
            return CreateTokenFailureResponse("Invalid request: missing Request Uri.");
        }

        try
        {
            var token = await cache.GetOrSetAsync(
                CacheKey,
                async ct => await tokenProvider.GetTokenAsync(ct),
                options => options
                    .SetDuration(TimeSpan.FromHours(2))
                    .SetFailSafe(true)
                    .SetFactoryTimeouts(TimeSpan.FromSeconds(10))
                    .SetEagerRefresh(0.9f),
                cancellationToken
            );

            return await AppendTokenAndSendAsync(request, token, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning("Failed to obtain CSRF token: {Error}", ex.Message);
            return CreateTokenFailureResponse(ex.Message);
        }
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
