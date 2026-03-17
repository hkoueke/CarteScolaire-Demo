using System.Globalization;
using CarteScolaire.Data.Queries;
using Lucene.Net.Index;
using Lucene.Net.Search;

namespace CarteScolaire.DataImpl.FuzzySearch.Strategies;

/// <summary>
/// FIXED: IsSatisfied now correctly guards against null DateOfBirth and requires DateFields.
/// Removed dead day/month range check (DateOnly constructor already validates).
/// BuildQuery message improved and made defensive.
/// </summary>
internal sealed class DateOnlyQueryStrategy<T> : QueryStrategyBase<T>
{
    public override bool CanApplyTo(SearchQuery query) => query.DateOfBirth.HasValue && DateFields.Count > 0;

    public override (Query query, Occur occur) BuildQuery(SearchQuery query)
    {
        // Defensive extraction + better exception message.
        // IsSatisfied now guarantees a value, but we stay extra safe.
        if (query.DateOfBirth is not { } dob)
        {
            throw new ArgumentNullException(nameof(query), "DateOfBirth must not be null when this strategy is used.");
        }

        string prefix = FormatDatePrefix(dob, query.DatePrecision);
        BooleanQuery block = [];

        foreach (FieldDescriptor f in DateFields)
        {
            Query dobQuery = query.DatePrecision == DatePrecision.Day
                ? new TermQuery(new Term(f.LuceneFieldName, prefix))
                : new PrefixQuery(new Term(f.LuceneFieldName, prefix));

            block.Add(dobQuery, Occur.SHOULD);
        }

        return (block, Occur.MUST);
    }

    private static string FormatDatePrefix(DateOnly date, DatePrecision precision) =>
        precision switch
        {
            DatePrecision.Year => date.ToString("yyyy", CultureInfo.InvariantCulture),
            DatePrecision.Month => date.ToString("yyyyMM", CultureInfo.InvariantCulture),
            _ => date.ToString("yyyyMMdd", CultureInfo.InvariantCulture)
        };
}