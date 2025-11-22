using System.Net;
using System.Text;
using CarteScolaire.Data.Responses;
using CarteScolaire.Data.Services;
using CarteScolaire.DataImpl.Handlers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ZiggyCreatures.Caching.Fusion;

namespace CarteScolaire.Tests.Unit;

public class TokenAppendingHandlerTests : IDisposable
{
    private readonly FusionCache _cache = new(new FusionCacheOptions());
    private readonly ITokenProvider<string> _tokenProvider = Substitute.For<ITokenProvider<string>>();
    private readonly ILogger<TokenAppendingHandler> _logger = Substitute.For<ILogger<TokenAppendingHandler>>();

    public void Dispose()
    {
        _cache.Dispose();
        GC.SuppressFinalize(this);
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
        var handler = CreateSut();
        var invoker = new HttpMessageInvoker(handler);
        var request = new HttpRequestMessage(HttpMethod.Get, (Uri?)null);

        var response = await invoker.SendAsync(request, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.ReasonPhrase.Should().Contain("Invalid request: missing Request Uri.");
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("Invalid request: missing Request Uri.");
    }

    [Fact]
    public async Task SendAsync_ShouldReturnError_WhenTokenProviderFails()
    {
        await _cache.SetAsync("csrf-token", Result<string>.Failure("Token fetch failed"), token: TestContext.Current.CancellationToken);
        var handler = CreateSut();
        var invoker = new HttpMessageInvoker(handler);
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri("https://test.example.com/api"));

        var response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("Token fetch failed");
    }

    [Fact]
    public async Task SendAsync_ShouldReturnError_WhenTokenIsNullOrEmpty()
    {
        await _cache.SetAsync("csrf-token", Result<string>.Success(string.Empty), token: TestContext.Current.CancellationToken);
        var handler = CreateSut();
        var invoker = new HttpMessageInvoker(handler);
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri("https://test.example.com/api"));

        var response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("Token provider returned a null or empty token");
    }

    [Fact]
    public async Task SendAsync_ShouldAppendTokenAndCallInnerHandler_WhenTokenIsValid()
    {
        const string token = "valid-csrf-token-value";
        await _cache.SetAsync("csrf-token", Result<string>.Success(token), token: TestContext.Current.CancellationToken);
        var handler = CreateSut();
        var invoker = new HttpMessageInvoker(handler);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://test.example.com/api?foo=bar");

        var response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
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

        _tokenProvider.GetTokenAsync(Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success(freshToken));

        var handler = CreateSut();
        var invoker = new HttpMessageInvoker(handler);
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri("https://api.test/api"));

        var response = await invoker.SendAsync(request, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        request.RequestUri!.Query.Should().Contain($"_token={freshToken}");

        // Verify it was cached
        var cached = await _cache.GetOrDefaultAsync<Result<string>>("csrf-token", token: TestContext.Current.CancellationToken);
        cached.Value.Should().Be(freshToken);

        // Second call should not hit tokenProvider again
        _tokenProvider.ClearReceivedCalls();
        var secondRequest = new HttpRequestMessage(HttpMethod.Get, new Uri("https://api.test/api"));
        await invoker.SendAsync(secondRequest, CancellationToken.None);
        await _tokenProvider.DidNotReceive().GetTokenAsync(TestContext.Current.CancellationToken);
    }
}

