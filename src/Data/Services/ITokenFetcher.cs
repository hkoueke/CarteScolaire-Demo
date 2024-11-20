namespace Data.Services;

/// <summary>
/// A simple token fetcher interface.
/// To effectively query the https://cartescolaire.cm API,
/// a token must be passed as part of the query parameters.
/// This interface fetches such a token.
/// </summary>
/// <typeparam name="T">The type of token that will be fetched.</typeparam>
public interface ITokenFetcher<T>
{
    /// <summary>
    /// Asynchronously fetches a token of type <typeparamref name="T"/> to be used in query parameters.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.
    /// The task result will contain a token of type <typeparamref name="T"/>.</returns>
    /// <exception cref="HttpRequestException"/>
    /// <exception cref="InvalidOperationException"/>
    /// <exception cref="TaskCanceledException"/>
    /// <exception cref="UriFormatException"/>
    public Task<T> FetchTokenAsync(CancellationToken cancellationToken = default);
}
