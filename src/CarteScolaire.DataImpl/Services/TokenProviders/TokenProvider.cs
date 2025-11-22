using System.Diagnostics;
using AngleSharp;
using CarteScolaire.Data.Responses;
using CarteScolaire.Data.Services;
using CarteScolaire.DataImpl.Helpers;
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
        //var baseAddress = httpClient.BaseAddress;//?.GetLeftPart(UriPartial.Authority);

        if (httpClient.BaseAddress is not null)
        {
            return await GetTokenThenParseAsync(cancellationToken);
        }

        logger.LogWarning("BaseAddress is not set on the HttpClient.");
        return Result<string>.Failure("BaseAddress is not set on the HttpClient.");
    }


    private async Task<Result<string>> GetTokenThenParseAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Initiating fetch for CSRF token from {BaseAddress} at {Path}",
            httpClient.BaseAddress, _options.TokenEndpointPath);

        var sw = Stopwatch.StartNew();

        var result = await ResultExtensions.TryAsync(
            async () =>
            {
                using var response = await httpClient
                    .GetAsync(_options.TokenEndpointPath, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    return Result<string>.Failure(
                        $"Failed to retrieve page for CSRF token extraction: {response.ReasonPhrase}"
                    );
                }

                var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

                using var document = await browsingContext
                    .OpenAsync(req => req.Content(stream, shouldDispose: true), cancellationToken)
                    .ConfigureAwait(false);

                logger.LogDebug("HTML document parsed successfully in {@ParseTime}", sw.Elapsed);

                return document
                    .QuerySelector(_options.TokenSelector)
                    .ToResult($"CSRF token element not found. Selector: {_options.TokenSelector}")
                    .Bind(element => element.GetAttribute("value").ToResult("CSRF token element found but value is missing"))
                    .Ensure(token => !string.IsNullOrWhiteSpace(token), "CSRF token element found but value is empty or missing");
            },
            ex => ex switch
            {
                HttpRequestException => $"Network error while fetching page for CSRF token from {httpClient.BaseAddress}: {ex.Message}",
                OperationCanceledException => $"Token fetch from {httpClient.BaseAddress} was canceled.",
                _ => $"Unexpected error during CSRF token extraction from {httpClient.BaseAddress}: {ex.Message}"
            }
        );

        sw.Stop();

        return result
            .OnSuccess(token =>
            {
                logger.LogInformation("CSRF token successfully extracted from {BaseAddress} in {@TotalTime}. Prefix: {TokenPrefix}, Length: {TokenLength}",
                    httpClient.BaseAddress,
                    sw.Elapsed,
                    token.Length >= 6 ? $"{token[..6]}..." : "## REDACTED ##",
                    token.Length
                );
            })
            .OnFailure(error => logger.LogWarning("{ErrorMessage} - Elapsed: {@Time}", error, sw.Elapsed));

    }
}