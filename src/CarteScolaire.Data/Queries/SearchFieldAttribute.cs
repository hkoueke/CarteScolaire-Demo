namespace CarteScolaire.Data.Queries;

public enum SearchFieldType
{
    /// <summary>Tokenized, analyzed text (e.g. full names). Supports fuzzy & partial matching.</summary>
    Text,

    /// <summary>Exact keyword match (e.g. gender, status codes). Not analyzed.</summary>
    Keyword,

    /// <summary>ISO-8601 date stored as yyyyMMdd for range queries.</summary>
    Date
}

/// <summary>
/// Marks a property on a POCO as a Lucene-indexed field.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SearchFieldAttribute : Attribute
{
    /// <summary>The Lucene field name. Defaults to the property name (lower-cased).</summary>
    public string? FieldName { get; set; }

    /// <summary>How the field value is analyzed and queried.</summary>
    public SearchFieldType FieldType { get; set; } = SearchFieldType.Text;

    /// <summary>
    /// Relevance multiplier applied during query building (1.0 = default).
    /// Boost names higher than secondary fields.
    /// </summary>
    public float Boost { get; set; } = 1.0f;

    /// <summary>
    /// When true the raw (un-analyzed) value is also stored so the original
    /// object can be reconstructed from the index without the source collection.
    /// </summary>
    public bool Store { get; set; } = true;
}
