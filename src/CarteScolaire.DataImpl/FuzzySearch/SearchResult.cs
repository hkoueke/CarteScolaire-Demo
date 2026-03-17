namespace CarteScolaire.DataImpl.FuzzySearch;

/// <summary>
/// A scored search result wrapping the original typed item.
/// </summary>
internal sealed class SearchResult<T>(T item, float score, int rank)
{
    /// <summary>The matched item from the source collection.</summary>
    public T Item { get; } = item;

    /// <summary>
    /// Lucene relevance score (higher = closer match).
    /// Normalised to [0, 1] relative to the top hit in the result set.
    /// </summary>
    public float Score { get; } = score;

    /// <summary>Rank within the result set (1-based).</summary>
    public int Rank { get; } = rank;

    public override string ToString() => $"[#{Rank} score={Score:F4}] {Item}";
}