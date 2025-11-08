using System.Diagnostics;
using AngleSharp;
using CarteScolaire.Data.Responses;
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

    public async Task<Result<string>> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        var baseAddress = httpClient.BaseAddress;//?.GetLeftPart(UriPartial.Authority);

        if (baseAddress is null)
        {
            return Result<string>.Failure("BaseAddress is not set on the HttpClient.");
        }

        logger.LogInformation("Initiating fetch for CSRF token from {BaseAddress} at {Path}", baseAddress, _options.TokenEndpointPath);

        var sw = Stopwatch.StartNew();

        try
        {
            using var response = await httpClient
                .GetAsync(_options.TokenEndpointPath, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var httpError = $"Failed to retrieve page for CSRF token extraction. HTTP status: {response.StatusCode} [{response.ReasonPhrase}]";
                logger.LogWarning("{ErrorMessage} from {BaseAddress}", httpError, baseAddress);
                return Result<string>.Failure(httpError);
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await browsingContext
                .OpenAsync(req => req.Content(stream), cancellationToken)
                .ConfigureAwait(false);

            logger.LogDebug("HTML document parsed successfully in {@ParseTime}", sw.Elapsed);

            var tokenElement = document.QuerySelector(_options.TokenSelector);
            if (tokenElement is null)
            {
                var selectorError = $"CSRF token input element not found. Selector: {_options.TokenSelector}";
                logger.LogWarning("{ErrorMessage} at {BaseAddress}", selectorError, baseAddress);
                return Result<string>.Failure(selectorError);
            }

            var token = tokenElement.GetAttribute("value");
            if (string.IsNullOrWhiteSpace(token))
            {
                var valueError = "CSRF token element found but value is empty or missing.";
                logger.LogWarning("{ErrorMessage} at {BaseAddress}", valueError, baseAddress);
                return Result<string>.Failure(valueError);
            }

            var tokenPrefix = token.Length >= 6 ? $"{token[..6]}..." : "## REDACTED ##";
            sw.Stop();

            logger.LogInformation("CSRF token successfully extracted from {BaseAddress} in {@TotalTime}. Prefix: {TokenPrefix}, Length: {TokenLength}",
                baseAddress, sw.Elapsed, tokenPrefix, token.Length);

            return token;
        }
        catch (Exception ex)
        {
            sw.Stop();
            var errorMessage = ex switch
            {
                HttpRequestException => $"Network error while fetching page for CSRF token from {baseAddress}",
                OperationCanceledException => $"Token fetch from {baseAddress} was canceled.",
                _ => $"Unexpected error during CSRF token extraction from {baseAddress}"
            };

            logger.LogError(ex, "{ErrorMessage} - Elapsed: {@Time}", errorMessage, sw.Elapsed);

            return Result<string>.Failure(errorMessage);
        }
    }
}