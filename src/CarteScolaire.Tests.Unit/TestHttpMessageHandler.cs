using System.Diagnostics.CodeAnalysis;

namespace CarteScolaire.Tests.Unit;

[ExcludeFromCodeCoverage]
internal sealed class TestHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => handler(request, cancellationToken);
}

