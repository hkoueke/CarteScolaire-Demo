using System.Diagnostics;
using AngleSharp;
using CarteScolaire.Data.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CarteScolaire.DataImpl.Services.TokenProviders;

internal sealed class TokenProvider(
    HttpClient httpClient,
    IBrowsingContext browsingContext,
    IOptions<TokenProviderOptions> options,
    ILogger<TokenProvider> logger) : ITokenProvider<string>
{
    private readonly TokenProviderOptions _options = options.Value;

    public async Task<string?> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        var baseAddress = httpClient.BaseAddress?.GetLeftPart(UriPartial.Authority) ??
                          throw new InvalidOperationException("BaseAddress is not set on the HttpClient.");

        logger.LogInformation("Initiating fetch for CSRF token from {BaseAddress} at {Path}", baseAddress, _options.TokenEndpointPath);

        var sw = Stopwatch.StartNew();

        try
        {
            using var response = await httpClient
                .GetAsync(_options.TokenEndpointPath, HttpCompletionOption.ResponseContentRead, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Failed to retrieve page for CSRF token extraction from {BaseAddress}. HTTP status: {StatusCode} ({ReasonPhrase})",
                    baseAddress, response.StatusCode, response.ReasonPhrase);
                return null;
            }

            logger.LogDebug("Page retrieved successfully from {BaseAddress} in {@ResponseTime}. Content length: {ContentLength} bytes",
                baseAddress, sw.Elapsed, response.Content.Headers.ContentLength ?? -1);

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

            using var document = await browsingContext
                .OpenAsync(req => req.Content(stream), cancellationToken)
                .ConfigureAwait(false);

            logger.LogDebug("HTML document parsed successfully in {@ParseTime}", sw.Elapsed);

            // Use the configured selector
            var tokenElement = document.QuerySelector(_options.TokenSelector);
            if (tokenElement is null)
            {
                logger.LogWarning("CSRF token input element not found in parsed HTML from {BaseAddress}. Selector: {Selector}",
                    baseAddress, _options.TokenSelector);
                return null;
            }

            var token = tokenElement.GetAttribute("value");
            if (string.IsNullOrWhiteSpace(token))
            {
                logger.LogWarning("CSRF token element found but value is empty or missing in HTML from {BaseAddress}", baseAddress);
                return null;
            }

            var tokenPrefix = token.Length >= 6 ? $"{token[..6]}..." : "## REDACTED ##";

            logger.LogInformation("CSRF token successfully extracted from {BaseAddress} in {@TotalTime}. Prefix: {TokenPrefix}, Length: {TokenLength}",
                baseAddress, sw.Elapsed, tokenPrefix, token.Length);

            return token;
        }
        catch (Exception ex)
        {
            var errorMessage = ex switch
            {
                HttpRequestException => $"Network error while fetching page for CSRF token from {baseAddress}",
                TaskCanceledException when cancellationToken.IsCancellationRequested => $"Token fetch canceled by request from {baseAddress}",
                TaskCanceledException => $"Timeout while fetching page for CSRF token from {baseAddress}",
                _ => $"Unexpected error during CSRF token extraction from {baseAddress}"
            };

            logger.LogError(ex, errorMessage);
            throw;
        }
    }
}
