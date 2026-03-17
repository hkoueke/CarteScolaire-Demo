using System.Net;
using System.Text;
using CarteScolaire.Data.Services;
using CarteScolaire.DataImpl.Handlers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ZiggyCreatures.Caching.Fusion;

namespace CarteScolaire.Tests.Unit;

public sealed class TokenAppendingHandlerTests : IDisposable
{
    private readonly FusionCache _cache = new(new FusionCacheOptions());
    private readonly ITokenProvider<string> _tokenProvider = Substitute.For<ITokenProvider<string>>();
    private readonly ILogger<TokenAppendingHandler> _logger = Substitute.For<ILogger<TokenAppendingHandler>>();

    public void Dispose()
    {
        _cache.Dispose();
    }

    private TokenAppendingHandler CreateSut(HttpMessageHandler? innerHandler = null)
    {
        innerHandler ??= new TestHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("OK", Encoding.UTF8, "text/plain")
            })
        );

        return new TokenAppendingHandler(_tokenProvider, _cache, _logger)
        {
            InnerHandler = innerHandler
        };
    }

    [Fact]
    public async Task SendAsync_ShouldReturnError_WhenRequestUriIsNull()
    {
        using TokenAppendingHandler handler = CreateSut();
        using HttpMessageInvoker invoker = new(handler);
        using HttpRequestMessage request = new(HttpMethod.Get, (Uri?)null);

        using HttpResponseMessage response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.ReasonPhrase.Should().Contain("Invalid request: missing Request Uri.");
        string content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("Invalid request: missing Request Uri.");
    }

    [Fact]
    public async Task SendAsync_ShouldReturnError_WhenTokenProviderFails()
    {
        _tokenProvider.GetTokenAsync(Arg.Any<CancellationToken>()).Throws<InvalidOperationException>();
        using TokenAppendingHandler handler = CreateSut();
        using HttpMessageInvoker invoker = new(handler);
        using HttpRequestMessage request = new(HttpMethod.Get, new Uri("https://test.example.com/api"));

        using HttpResponseMessage response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task SendAsync_ShouldAppendTokenAndCallInnerHandler_WhenTokenIsValid()
    {
        const string token = "valid-csrf-token-value";
        await _cache.SetAsync("csrf-token", token, token: TestContext.Current.CancellationToken);
        using TokenAppendingHandler handler = CreateSut();
        using HttpMessageInvoker invoker = new(handler);
        using HttpRequestMessage request = new(HttpMethod.Get, new Uri("https://test.example.com/api?foo=bar"));

        using HttpResponseMessage response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        string content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Be("OK");

        // Vérifier que le token est bien ajouté à l'URL
        request.RequestUri!.OriginalString.Should().Contain($"_token={token}");
        request.RequestUri!.Query.Should().Contain("foo=bar");
        request.RequestUri!.Query.Should().Contain("_token=");
    }

    [Fact]
    public async Task SendAsync_ShouldFetchAndCacheToken_WhenCacheIsEmpty()
    {
        const string freshToken = "fresh-token-789";
        _tokenProvider.GetTokenAsync(Arg.Any<CancellationToken>()).Returns(freshToken);

        using TokenAppendingHandler handler = CreateSut();
        using HttpMessageInvoker invoker = new(handler);
        using HttpRequestMessage request = new(HttpMethod.Get, new Uri("https://api.test/api"));

        using HttpResponseMessage response = await invoker.SendAsync(request, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        request.RequestUri!.Query.Should().Contain($"_token={freshToken}");

        // Verify it was cached
        string? cached = await _cache.GetOrDefaultAsync<string>("csrf-token", token: TestContext.Current.CancellationToken);
        cached.Should().Be(freshToken);

        // Second call should not hit tokenProvider again
        _tokenProvider.ClearReceivedCalls();
        await invoker.SendAsync(request, CancellationToken.None);
        await _tokenProvider.DidNotReceive().GetTokenAsync(TestContext.Current.CancellationToken);
    }
}
