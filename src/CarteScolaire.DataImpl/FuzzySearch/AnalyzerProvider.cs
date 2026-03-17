using CarteScolaire.Data.Queries;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Core;
using Lucene.Net.Analysis.Miscellaneous;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Util;

namespace CarteScolaire.DataImpl.FuzzySearch;

/// <summary>
/// Builds a PerFieldAnalyzerWrapper that applies the most appropriate analyzer per field.
/// </summary>
internal static class AnalyzerProvider
{
    public static Analyzer BuildForType<T>(LuceneVersion version)
    {
        IReadOnlyList<FieldDescriptor> fields = FieldMetadataCache.Instance.GetFields<T>();
        Dictionary<string, Analyzer> map = [];
#pragma warning disable CA2000
        StandardAnalyzer defaultAnalyzer = new(version);
#pragma warning restore CA2000
        try
        {
            foreach (FieldDescriptor f in fields)
            {
                map[f.LuceneFieldName] = f.FieldType switch
                {
                    SearchFieldType.Keyword or SearchFieldType.Date => new KeywordAnalyzer(),
                    _ => new StandardAnalyzer(version)
                };
            }
            return new PerFieldAnalyzerWrapper(defaultAnalyzer, map);
        }
        catch
        {
            defaultAnalyzer.Dispose();
            foreach (Analyzer analyzer in map.Values)
                analyzer.Dispose();
            throw;
        }
    }
}