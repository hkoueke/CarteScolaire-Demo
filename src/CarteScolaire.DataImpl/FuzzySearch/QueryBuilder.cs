using CarteScolaire.Data.Queries;
using CarteScolaire.DataImpl.FuzzySearch.Strategies;
using Lucene.Net.Search;

namespace CarteScolaire.DataImpl.FuzzySearch;

/// <summary>
/// Translates a <see cref="SearchQuery"/> into a Lucene <see cref="Query"/>.
/// </summary>
internal static class QueryBuilder<T>
{
    /// <summary>
    /// Builds a Lucene <see cref="Query"/> from the supplied <paramref name="searchQuery"/>.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Thrown when no searchable field is populated on <paramref name="searchQuery"/>.
    /// </exception>
    public static Query Build(SearchQuery searchQuery, QueryStrategyBase<T>[] queryStrategies)
    {
        ArgumentNullException.ThrowIfNull(searchQuery);
        ArgumentNullException.ThrowIfNull(queryStrategies);

        (Query, Occur)[] results = queryStrategies
            .Where(s => s.CanApplyTo(searchQuery))
            .Select(s => s.BuildQuery(searchQuery))
            .ToArray();

        if (results.Length == 0)
        {
            throw new ArgumentException("SearchQuery must have at least one non-null field.", nameof(searchQuery));
        }

        BooleanQuery root = [];

        foreach ((Query query, Occur occur) in results)
        {
            root.Add(query, occur);
        }

        return root;
    }
}