namespace CarteScolaire.Data.Services;

/// <summary>
/// A simple abstraction representing an HTML parser.
/// </summary>
/// <typeparam name="T">The type of the class into which the HTML will be parsed. This must be a reference type.</typeparam>
public interface IHtmlParser<T> where T : notnull
{
    /// <summary>
    /// Asynchronously parses student information from HTML content using the specified XPath query.
    /// </summary>
    /// <param name="stream">The input stream containing the HTML. Must not be <c>null</c>.</param>
    /// <param name="cancellationToken">Token to observe for cancellation. Optional.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a read-only collection of parsed <see cref="StudentInfoResponse"/> objects.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="stream"/> is <c>null</c>.</exception>
    /// <exception cref="OperationCanceledException">Thrown if the operation is canceled.</exception>
    /// <exception cref="InvalidOperationException">Thrown if parsing the HTML fails.</exception>
    public Task<IReadOnlyCollection<T>> ParseAsync(Stream stream, CancellationToken cancellationToken = default);
}
