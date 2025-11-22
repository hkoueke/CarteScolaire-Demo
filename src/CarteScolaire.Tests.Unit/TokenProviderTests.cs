using AngleSharp;
using CarteScolaire.DataImpl.Services.TokenProviders;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Net;
using System.Text;

namespace CarteScolaire.Tests.Unit;

public class TokenProviderTests : IDisposable
{
    private readonly TokenProviderOptions _options = new()
    {
        TokenEndpointPath = "/token",
        TokenSelector = "#csrf"
    };

    private readonly IBrowsingContext _browsingContext = BrowsingContext.New(Configuration.Default.WithDefaultLoader());
    private readonly ILogger<TokenProvider> _logger = Substitute.For<ILogger<TokenProvider>>();

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _browsingContext.Dispose();
    }


    private TokenProvider CreateSut(HttpClient httpClient) 
        => new(httpClient, _browsingContext, Options.Create(_options), _logger);


    private static HttpClient HttpClientFromHtml(string html, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var handler = new TestHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(html, Encoding.UTF8, "text/html")
            }));

        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://test.example.com")
        };

        return client;
    }

    private static HttpClient HttpClientWithException(Exception exception)
    {
        var handler = new TestHttpMessageHandler((_, _) => Task.FromException<HttpResponseMessage>(exception));

        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://test.example.com")
        };

        return client;
    }

    [Fact]
    public async Task GetTokenAsync_ShouldReturnFailure_WhenBaseAddressIsNull()
    {
        var httpClient = new HttpClient { BaseAddress = null! };
        var provider = CreateSut(httpClient);

        var result = await provider.GetTokenAsync(TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("BaseAddress is not set on the HttpClient");
    }

    [Fact]
    public async Task GetTokenAsync_ShouldReturnSuccess_WhenTokenIsExtracted()
    {
        const string token = "abc123xyz";
        const string html = 
            $"""
                <!DOCTYPE html>
                <html><body>
                    <input id="csrf" value="{token}" />
                </body></html>
              """;

        using var httpClient = HttpClientFromHtml(html);
        var provider = CreateSut(httpClient);

        var result = await provider.GetTokenAsync(TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(token);
    }

    [Fact]
    public async Task GetTokenAsync_ShouldReturnFailure_WhenHttpResponseIsNotSuccess()
    {
        using var httpClient = HttpClientFromHtml("", HttpStatusCode.BadRequest);
        var provider = CreateSut(httpClient);

        var result = await provider.GetTokenAsync(TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Bad Request");
    }

    [Fact]
    public async Task GetTokenAsync_ShouldReturnFailure_WhenTokenElementNotFound()
    {
        const string html = "<!DOCTYPE html><html><body>No token here</body></html>";

        using var httpClient = HttpClientFromHtml(html);
        var provider = CreateSut(httpClient);

        var result = await provider.GetTokenAsync(TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("CSRF token element not found. Selector: #csrf");
    }

    [Fact]
    public async Task GetTokenAsync_ShouldReturnFailure_WhenHttpRequestExceptionOccurs()
    {
        using var httpClient = HttpClientWithException(new HttpRequestException("Network failure"));
        var provider = CreateSut(httpClient);

        var result = await provider.GetTokenAsync(TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Network error while fetching page for CSRF token");
    }

    [Fact]
    public async Task GetTokenAsync_ShouldReturnFailure_WhenOperationCanceledExceptionOccurs()
    {
        using var httpClient = HttpClientWithException(new OperationCanceledException("Cancelled"));
        var provider = CreateSut(httpClient);

        var result = await provider.GetTokenAsync(TestContext.Current.CancellationToken);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Token fetch from https://test.example.com/ was canceled");
    }
}
