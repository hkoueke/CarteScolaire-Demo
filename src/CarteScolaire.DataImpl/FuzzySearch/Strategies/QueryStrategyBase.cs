using CarteScolaire.Data.Queries;
using Lucene.Net.Search;

namespace CarteScolaire.DataImpl.FuzzySearch.Strategies;

internal abstract class QueryStrategyBase<T>
{
    /// <summary>
    /// Levenshtein distance ceiling for fuzzy matching.
    /// </summary>
    protected const int FuzzyMaxEdits = 2;

    /// <summary>
    /// Static per closed generic type — computed once, shared across all instances.
    /// <remarks>
    /// Avoids repeated LINQ scans on every Build() call.
    /// </remarks>
    /// </summary>
    protected static readonly IReadOnlyList<FieldDescriptor> TextFields =
        FieldMetadataCache.Instance.GetFields<T>()
            .Where(f => f.FieldType == SearchFieldType.Text)
            .ToList();

    protected static readonly IReadOnlyList<FieldDescriptor> DateFields =
        FieldMetadataCache.Instance.GetFields<T>()
            .Where(f => f.FieldType == SearchFieldType.Date)
            .ToList();

    protected static readonly IReadOnlyList<FieldDescriptor> KeywordFields =
        FieldMetadataCache.Instance.GetFields<T>()
            .Where(f => f.FieldType == SearchFieldType.Keyword)
            .ToList();

    public abstract bool CanApplyTo(SearchQuery query);

    public abstract (Query query, Occur occur) BuildQuery(SearchQuery query);
}