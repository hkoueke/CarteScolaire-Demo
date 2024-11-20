using Data.Services;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace DataImpl.Services;

internal sealed class StringTokenFetcher(HttpClient httpClient, ILogger<StringTokenFetcher> logger) : ITokenFetcher<string>
{
    public async Task<string> FetchTokenAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Fetching Token at {BaseAddress}...", httpClient.BaseAddress);

        HttpResponseMessage response = await httpClient.GetAsync("/", cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Request to {Uri} has failed: Status code {StatusCode} | Reason : {Reason}",
                httpClient.BaseAddress,
                response.StatusCode,
                response.ReasonPhrase);
            return string.Empty;
        }

        var pageContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        // Load the content into HtmlAgilityPack to parse the HTML
        var htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(pageContent);

        // Use XPath or CSS selectors to find the hidden input field
        HtmlNode? tokenNode = htmlDoc.DocumentNode.SelectSingleNode("//input[@name='_token']");

        if (tokenNode is null)
        {
            logger.LogError("Failed to locate the hidden input field with name '_token' in the HTML document. Please check the structure of the HTML.");
        }

        // Get the value of the _token, or a default string
        return tokenNode?.GetAttributeValue("value", string.Empty) ?? string.Empty;
    }
}
