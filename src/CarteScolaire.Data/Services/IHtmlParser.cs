using CarteScolaire.Data.Responses;

namespace CarteScolaire.Data.Services;

/// <summary>
/// A simple abstraction representing an HTML parser.
/// </summary>
/// <typeparam name="T">The type of the class into which the HTML will be parsed.
/// This must be a reference type.</typeparam>
public interface IHtmlParser<T> where T : notnull
{
    /// <summary>
    /// Asynchronously parses data from an input stream.
    /// </summary>
    /// <typeparam name="T">The type of the successful parsing result.</typeparam>
    /// <param name="stream">The input stream containing the data to be parsed.</param>
    /// <param name="cancellationToken">Token to observe for cancellation. Optional.</param>
    /// <returns>A task that represents the asynchronous operation. 
    /// The task result contains a <see cref="Result{IReadOnlyCollection{T}}"/> indicating success with the parsed data 
    /// or a failure with an error message.</returns>
    public Task<Result<IReadOnlyCollection<T>>> ParseAsync(Stream stream, CancellationToken cancellationToken = default);
}
//todo: fix comments