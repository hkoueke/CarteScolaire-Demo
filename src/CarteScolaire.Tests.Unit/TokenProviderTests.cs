using System.Net;
using System.Text;
using AngleSharp;
using CarteScolaire.DataImpl.Services.TokenProviders;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace CarteScolaire.Tests.Unit;

public sealed class TokenProviderTests : IDisposable
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
        _browsingContext.Dispose();
    }


    private TokenProvider CreateSut(HttpClient httpClient)
        => new(httpClient, _browsingContext, Options.Create(_options), _logger);


    private static HttpClient HttpClientFromHtml(string html, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        using TestHttpMessageHandler handler = new((_, _) =>
             Task.FromResult(new HttpResponseMessage(statusCode)
             {
                 Content = new StringContent(html, Encoding.UTF8, "text/html")
             }));

        return new HttpClient(handler)
        {
            BaseAddress = new Uri("https://test.example.com")
        };
    }

    private static HttpClient HttpClientWithException(Exception exception)
    {
        using TestHttpMessageHandler handler = new((_, _) => Task.FromException<HttpResponseMessage>(exception));

        return new HttpClient(handler)
        {
            BaseAddress = new Uri("https://test.example.com")
        };
    }

    [Fact]
    public async Task GetTokenAsync_ShouldThrow_WhenBaseAddressIsNull()
    {
        using HttpClient httpClient = new();
        httpClient.BaseAddress = null!;
        TokenProvider provider = CreateSut(httpClient);

        await provider
            .Invoking(p => p.GetTokenAsync(TestContext.Current.CancellationToken))
            .Should().ThrowAsync<UriFormatException>()
            .WithMessage("*BaseAddress is not set on the HttpClient*");

        _logger.Received(1).Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("BaseAddress is not set on the HttpClient")),
            null,
            Arg.Any<Func<object, Exception?, string>>());
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

        using HttpClient httpClient = HttpClientFromHtml(html);
        TokenProvider provider = CreateSut(httpClient);

        string result = await provider.GetTokenAsync(TestContext.Current.CancellationToken);

        result.Should().Be(token);
    }

    [Fact]
    public async Task GetTokenAsync_ShouldThrow_WhenHttpResponseIsNotSuccess()
    {
        using HttpClient httpClient = HttpClientFromHtml("", HttpStatusCode.BadRequest);
        TokenProvider provider = CreateSut(httpClient);

        await provider
            .Invoking(p => p.GetTokenAsync(TestContext.Current.CancellationToken))
            .Should().ThrowAsync<HttpRequestException>()
            .WithMessage("*400 (Bad Request)*");
    }

    [Fact]
    public async Task GetTokenAsync_ShouldThrow_WhenTokenElementNotFound()
    {
        const string html = "<!DOCTYPE html><html><body>No token here</body></html>";
        using HttpClient httpClient = HttpClientFromHtml(html);
        TokenProvider provider = CreateSut(httpClient);

        await provider
            .Invoking(p => p.GetTokenAsync(TestContext.Current.CancellationToken))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("CSRF token element not found. Selector: #csrf");
    }

    [Fact]
    public async Task GetTokenAsync_ShouldThrow_WhenHttpRequestExceptionOccurs()
    {
        using HttpClient httpClient = HttpClientWithException(new HttpRequestException("Network failure"));
        TokenProvider provider = CreateSut(httpClient);

        await provider
            .Invoking(p => p.GetTokenAsync(TestContext.Current.CancellationToken))
            .Should().ThrowAsync<HttpRequestException>()
            .WithMessage("*Network failure*");
    }

    [Fact]
    public async Task GetTokenAsync_ShouldThrow_WhenOperationCanceledExceptionOccurs()
    {
        using HttpClient httpClient = HttpClientWithException(new OperationCanceledException("Operation canceled"));
        TokenProvider provider = CreateSut(httpClient);

        await provider
            .Invoking(p => p.GetTokenAsync(TestContext.Current.CancellationToken))
            .Should().ThrowAsync<OperationCanceledException>()
            .WithMessage("*Operation canceled*");
    }
}
