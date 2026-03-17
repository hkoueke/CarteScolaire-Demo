using CarteScolaire.Data.Queries;
using CarteScolaire.Data.Responses;
using Lucene.Net.Index;
using Lucene.Net.Search;

namespace CarteScolaire.DataImpl.FuzzySearch.Strategies;

/// <summary>
/// FIXED: IsSatisfied was incorrectly checking DateFields instead of KeywordFields.
/// BuildQuery exception message improved for consistency.
/// Unspecified-gender boosting logic left unchanged (as originally intended).
/// </summary>
internal sealed class EnumQueryStrategy<T> : QueryStrategyBase<T>
{
    // All known Gender values except Unspecified, computed once at class initialisation.
    private readonly IReadOnlyList<Gender> _knownGenders =
        Enum.GetValues<Gender>()
            .Where(g => g != Gender.Unspecified)
            .ToList();

    public override (Query query, Occur occur) BuildQuery(SearchQuery query)
    {
        if (query.Gender is null)
            throw new ArgumentNullException(nameof(query), "Gender must not be null when this strategy is used.");

        bool isSpecified = query.Gender != Gender.Unspecified;
        BooleanQuery block = [];

        IEnumerable<Gender> genders = isSpecified ? [query.Gender.Value] : _knownGenders;

        foreach (Gender g in genders)
        {
            foreach (FieldDescriptor f in KeywordFields)
            {
                block.Add(
                    new TermQuery(new Term(f.LuceneFieldName, g.ToString().ToLowerInvariant())),
                    Occur.SHOULD);
            }
        }

        return (block, isSpecified ? Occur.MUST : Occur.SHOULD);
    }

    public override bool CanApplyTo(SearchQuery query) => query.Gender.HasValue && KeywordFields.Count > 0;
}