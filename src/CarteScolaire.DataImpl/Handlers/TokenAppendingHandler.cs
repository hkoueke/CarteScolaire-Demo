using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
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

        string token;

        try
        {
            token = await cache.GetOrSetAsync(
                CacheKey,
                async _ =>
                {
                    var result = await _tokenProvider.GetTokenAsync(cancellationToken).ConfigureAwait(false);

                    if (result.IsFailure)
                    {
                        logger.LogError("Token provider failed to retrieve token. Reason: {Error}", result.Error);
                        throw new TokenRetrievalException(result.Error);
                    }

                    return result.Value;
                },
                options => options
                    .SetDuration(TimeSpan.FromHours(2))
                    .SetFailSafe(true)
                    .SetFactoryTimeouts(TimeSpan.FromSeconds(10))
                    .SetEagerRefresh(0.9f), cancellationToken
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to acquire CSRF token.");
            return CreateTokenFailureResponse($"Failed to acquire CSRF token. Reason: {ex.Message}");
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            logger.LogError("Token acquisition (including cache/fail-safe) resulted in a null or empty token.");
            return CreateTokenFailureResponse("Token acquisition failed, resulting in an empty token.");
        }

        var uriBuilder = new UriBuilder(request.RequestUri);
        NameValueCollection parts = HttpUtility.ParseQueryString(uriBuilder.Query);
        parts["_token"] = token;
        uriBuilder.Query = parts.ToString();
        request.RequestUri = uriBuilder.Uri;

        // Proceed with the modified request
        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private static HttpResponseMessage CreateTokenFailureResponse(string message) => new(System.Net.HttpStatusCode.InternalServerError)
    {
        ReasonPhrase = message,
        Content = new StringContent(message, System.Text.Encoding.UTF8, "text/plain")
    };
}

/// <summary>
/// Custom exception to signal a failure when unwrapping the Result<T> from the token provider.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class TokenRetrievalException(string? message) : Exception(message);
