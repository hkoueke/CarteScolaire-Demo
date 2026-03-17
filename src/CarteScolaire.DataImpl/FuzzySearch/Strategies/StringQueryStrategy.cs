using CarteScolaire.Data.Queries;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.TokenAttributes;
using Lucene.Net.Index;
using Lucene.Net.Search;

namespace CarteScolaire.DataImpl.FuzzySearch.Strategies;

internal sealed class StringQueryStrategy<T>(Analyzer analyzer) : QueryStrategyBase<T>
{
    public override bool CanApplyTo(SearchQuery query) 
        => !string.IsNullOrWhiteSpace(query.Name.Trim()) && TextFields.Count > 0;

    public override (Query query, Occur occur) BuildQuery(SearchQuery query)
    {
        // Tokenize once against the first field — behaviour is identical across all text fields.
        string[] tokens = Tokenize(query.Name.Trim(), TextFields[0].LuceneFieldName)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToArray();

        int editDistance = query.Fuzziness > 0f ? FuzzyMaxEdits : 0;
        BooleanQuery outer = [];

        foreach (FieldDescriptor field in TextFields)
        {
            BooleanQuery fieldQuery = BuildFieldNameQuery(field, tokens, editDistance);

            if (fieldQuery.GetClauses().Length > 0)
            {
                outer.Add(fieldQuery, Occur.SHOULD);
            }
        }

        return (outer, Occur.SHOULD);
    }

    private static BooleanQuery BuildFieldNameQuery(FieldDescriptor field, string[] tokens, int editDistance)
    {
        BooleanQuery fieldQuery = [];

        // 1. Exact phrase — highest boost
        if (tokens.Length > 0)
        {
            PhraseQuery phrase = [];
            foreach (string token in tokens)
            {
                phrase.Add(new Term(field.LuceneFieldName, token));
            }

            phrase.Boost = field.Boost * 4.0f;
            fieldQuery.Add(phrase, Occur.SHOULD);
        }

        // 2. Per-token fuzzy (or exact when fuzziness is off) + prefix
        foreach (string token in tokens)
        {
            Query tokenQuery = editDistance > 0
                ? new FuzzyQuery(
                        new Term(field.LuceneFieldName, token),
                        maxEdits: editDistance,
                        prefixLength: Math.Max(1, token.Length / 3))
                {
                    Boost = field.Boost * 2.0f
                }
                : new TermQuery(new Term(field.LuceneFieldName, token))
                {
                    Boost = field.Boost * 2.5f
                };

            fieldQuery.Add(tokenQuery, Occur.SHOULD);
            fieldQuery.Add(new PrefixQuery(new Term(field.LuceneFieldName, token)) { Boost = field.Boost * 1.5f }, Occur.SHOULD);
        }

        return fieldQuery;
    }

    private string[] Tokenize(string text, string fieldName)
    {
        List<string> tokens = [];
        using TokenStream ts = analyzer.GetTokenStream(fieldName, new StringReader(text));
        ICharTermAttribute termAttr = ts.GetAttribute<ICharTermAttribute>();
        ts.Reset();

        while (ts.IncrementToken())
        {
            tokens.Add(termAttr.ToString());
        }

        ts.End();
        return [..tokens];
    }
}