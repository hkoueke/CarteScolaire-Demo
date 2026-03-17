namespace CarteScolaire.Data.Queries;

/// <summary>
/// Controls how strictly the <see cref="SearchQuery.DateOfBirth"/> filter is applied.
///
/// The stored date format is always yyyyMMdd, so precision maps directly
/// to a prefix length used in a Lucene <c>PrefixQuery</c>:
///
///   Year  → prefix "yyyy"     (4 chars) — matches any day in that year
///   Month → prefix "yyyyMM"   (6 chars) — matches any day in that month
///   Day   → exact  "yyyyMMdd" (8 chars) — matches only that exact date
///
/// Use <see cref="Year"/> or <see cref="Month"/> when scraped data contains
/// partial dates, or when the query source only has year/month granularity.
/// </summary>
public enum DatePrecision
{
    /// <summary>Match documents whose DOB falls in the same year.</summary>
    Year,

    /// <summary>Match documents whose DOB falls in the same year and month.</summary>
    Month,

    /// <summary>Match documents whose DOB is exactly this date (default).</summary>
    Day
}
