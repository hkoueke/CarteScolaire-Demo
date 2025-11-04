namespace CarteScolaire.Data.Services;

/// <summary>
/// A simple token provider interface.
/// To effectively query the https://cartescolaire.cm API,
/// a token must be passed as part of the query parameters.
/// This interface provides such a token.
/// </summary>
/// <typeparam name="T">The type of token that will be provided.</typeparam>
public interface ITokenProvider<T> where T: class
{
    /// <summary>
    /// Asynchronously provides a token of type <typeparamref name="T"/> to be used in query parameters.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The CSRF token if found; otherwise, null.</returns>
    /// <exception cref="HttpRequestException"/>
    /// <exception cref="InvalidOperationException"/>
    /// <exception cref="TaskCanceledException"/>
    /// <exception cref="UriFormatException"/>
    public Task<T?> GetTokenAsync(CancellationToken cancellationToken = default);
}
