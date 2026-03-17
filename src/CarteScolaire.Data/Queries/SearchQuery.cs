using CarteScolaire.Data.Responses;

namespace CarteScolaire.Data.Queries;

/// <summary>
/// Represents a structured student search query. Some fields are optional;
/// only populated fields contribute to scoring/filtering.
/// </summary>
/// <summary>
/// <remarks>
/// This class is Immutable — use <c>with</c> expressions to derive variants.
/// </remarks>
/// </summary>
public sealed record SearchQuery
{
    /// <summary>
    /// School ID passed directly to the external service. Not used for fuzzy matching.
    /// </summary>
    public string SchoolId { get; init; } = string.Empty;

    /// <summary>Full or partial name. Supports fuzzy matching and phonetic variants.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Date of birth. Treated as an exact-match filter when provided.</summary>
    public DateOnly? DateOfBirth { get; init; }

    /// <summary>
    /// How precisely to apply the DOB filter. Default is <see cref="DatePrecision.Day"/>
    /// (exact match). Use <see cref="DatePrecision.Year"/> or <see cref="DatePrecision.Month"/>
    /// when the query or the scraped data only has partial date information.
    /// </summary>
    public DatePrecision DatePrecision { get; init; } = DatePrecision.Day;

    /// <summary>Gender string (e.g. "M", "F", "Male", "Female"). Case-insensitive exact match.</summary>
    public Gender? Gender { get; init; } = Responses.Gender.Unspecified;

    /// <summary>
    /// 0–1 fuzzy edit distance ratio for name matching (0 = exact, 1 = very loose).
    /// Maps to Lucene FuzzyQuery MAX_EDITS. Values: 0.0 → 0 edits, &gt;0 → 2 edits.
    /// Default: 0.7 (equivalent to ~2 Levenshtein edits for typical names).
    /// </summary>
    public float Fuzziness { get; init; } = 0.7f;

    /// <summary>Maximum number of hits to return. Default 50.</summary>
    public int MaxResults { get; init; } = 50;

}