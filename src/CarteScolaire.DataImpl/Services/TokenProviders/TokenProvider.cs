using System.Diagnostics;
using AngleSharp;
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

    public async Task<string> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        if (httpClient.BaseAddress is not null)
            return await GetTokenThenParseAsync(cancellationToken);

        logger.LogWarning("BaseAddress is not set on the HttpClient.");
        throw new UriFormatException("BaseAddress is not set on the HttpClient.");
    }

    private async Task<string> GetTokenThenParseAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Initiating fetch for CSRF token from {BaseAddress} at {Path}", httpClient.BaseAddress, _options.TokenEndpointPath);

        var sw = Stopwatch.StartNew();

        using var response = await httpClient
            .GetAsync(_options.TokenEndpointPath, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        using var document = await browsingContext
            .OpenAsync(req => req.Content(stream, shouldDispose: true), cancellationToken)
            .ConfigureAwait(false);

        logger.LogDebug("HTML document parsed successfully in {@ParseTime}", sw.Elapsed);

        var parseResult = document
            .QuerySelector(_options.TokenSelector)
            .ToResult($"CSRF token element not found. Selector: {_options.TokenSelector}")
            .Bind(element => element.GetAttribute("value").ToResult("CSRF token element found but value is missing"))
            .Ensure(token => !string.IsNullOrWhiteSpace(token), "CSRF token element found but value is empty or missing");

        sw.Stop();

        if (parseResult.IsSuccess)
        {
            logger.LogInformation(
                "CSRF token successfully extracted from {BaseAddress} in {@TotalTime}. Prefix: {TokenPrefix}, Length: {TokenLength}",
                httpClient.BaseAddress,
                sw.Elapsed,
                parseResult.Value.Length >= 6 ? $"{parseResult.Value[..6]}..." : "## REDACTED ##",
                parseResult.Value.Length);

            return parseResult.Value;
        }

        logger.LogWarning("{ErrorMessage} - Elapsed: {@Time}", parseResult.Error, sw.Elapsed);
        throw new InvalidOperationException(parseResult.Error);
    }
}
