namespace Data.Services;

/// <summary>
/// A simple abstraction representing an HTML parser.
/// </summary>
/// <typeparam name="T">The type of the class into which the HTML will be parsed. This must be a reference type.</typeparam>
public interface IHtmlParser<T> where T : class
{
    /// <summary>
    /// Parses the given HTML string and returns a collection of parsed results.
    /// </summary>
    /// <param name="html">The HTML string to be parsed.</param>
    /// <returns>An <see cref="IEnumerable{T}"/> containing the parsed results from the HTML.</returns>
    /// <exception cref="ArgumentException"/>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="OperationCanceledException"/>
    public Task<IEnumerable<T>> ParseAsync(string html, CancellationToken cancellationToken = default);
}
