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
    private const string TokenQueryKey = "_token"; // Made constant for easy config change

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri is null)
        {
            logger.LogWarning("RequestUri is null. Ensure 'BaseAddress' is configured on the HttpClient.");
            return CreateTokenFailureResponse("Invalid request: missing Request Uri.");
        }

        try
        {
            string token = await cache.GetOrSetAsync(
                CacheKey,
                async ct => await tokenProvider.GetTokenAsync(ct).ConfigureAwait(false),
                options => options
                    .SetDuration(TimeSpan.FromHours(2))
                    .SetFailSafe(true)
                    .SetFactoryTimeouts(TimeSpan.FromSeconds(10))
                    .SetEagerRefresh(0.9f),
                cancellationToken
            ).ConfigureAwait(false);

            return await AppendTokenAndSendAsync(request, token, cancellationToken).ConfigureAwait(false);
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            logger.LogWarning(ex, "Failed to obtain CSRF token");
            return CreateTokenFailureResponse(ex.Message);
        }
    }

    private Task<HttpResponseMessage> AppendTokenAndSendAsync(HttpRequestMessage request, string token, CancellationToken cancellationToken)
    {
        UriBuilder uriBuilder = new(request.RequestUri!);
        NameValueCollection parts = HttpUtility.ParseQueryString(uriBuilder.Query);

        parts[TokenQueryKey] = token;
        uriBuilder.Query = parts.ToString() ?? string.Empty;
        request.RequestUri = uriBuilder.Uri;

        // Proceed with the modified request
        return base.SendAsync(request, cancellationToken);
    }

    private static HttpResponseMessage CreateTokenFailureResponse(string? message)
    {
        string text = string.IsNullOrEmpty(message)
            ? "An unknown error occurred while retrieving the CSRF token."
            : message;

        return new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            ReasonPhrase = text,
            Content = new StringContent(text, Encoding.UTF8, "text/plain")
        };
    }
}
