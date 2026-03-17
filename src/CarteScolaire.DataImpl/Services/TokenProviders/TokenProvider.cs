using System.Diagnostics;
using AngleSharp;
using AngleSharp.Dom;
using CarteScolaire.Data.Responses;
using CarteScolaire.Data.Services;
using CarteScolaire.DataImpl.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CarteScolaire.DataImpl.Services.TokenProviders;

#pragma warning disable CA2007

internal sealed class TokenProvider(
    HttpClient httpClient,
    IBrowsingContext browsingContext,
    IOptions<TokenProviderOptions> options,
    ILogger<TokenProvider> logger) : ITokenProvider<string>
{
    private readonly TokenProviderOptions _options = options.Value;

    public async Task<string> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        if (httpClient.BaseAddress is null)
        {
            logger.LogWarning("BaseAddress is not set on the HttpClient.");
            throw new UriFormatException("BaseAddress is not set on the HttpClient.");
        }

        return await GetTokenThenParseAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> GetTokenThenParseAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Initiating fetch for CSRF token from {BaseAddress} at {Path}",
            httpClient.BaseAddress, _options.TokenEndpointPath);

        Stopwatch sw = Stopwatch.StartNew();

        using HttpResponseMessage response = await httpClient
            .GetAsync(new Uri(_options.TokenEndpointPath, UriKind.Relative),
                      HttpCompletionOption.ResponseHeadersRead,
                      cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        await using Stream stream = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);

        using IDocument document = await browsingContext
            .OpenAsync(req => req.Content(stream), cancellationToken)
            .ConfigureAwait(false);

        logger.LogDebug("HTML document parsed successfully in {@ParseTime}", sw.Elapsed);

        Result<string> parseResult = document
            .QuerySelector(_options.TokenSelector)
            .ToResult($"CSRF token element not found. Selector: {_options.TokenSelector}")
            .Bind(element => element.GetAttribute("value")
                .ToResult("CSRF token element found but value is missing"))
            .Ensure(token => !string.IsNullOrWhiteSpace(token), "CSRF token element found but value is empty");

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