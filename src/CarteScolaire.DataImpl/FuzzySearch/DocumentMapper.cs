using System.Globalization;
using CarteScolaire.Data.Queries;
using Lucene.Net.Documents;

namespace CarteScolaire.DataImpl.FuzzySearch;

/// <summary>
/// Converts a POCO of type <typeparamref name="T"/> into a Lucene Document and back.
/// </summary>
internal sealed class DocumentMapper<T>
{
    // Internal field that holds the original item's index in the source list.
    private const string IdField = "__id";

    private readonly IReadOnlyList<FieldDescriptor> _fields = FieldMetadataCache.Instance.GetFields<T>();

    // ---- T → Document ----
    public Document ToDocument(T item, int sourceIndex)
    {
        ArgumentNullException.ThrowIfNull(item);

        Document doc = [new StoredField(IdField, sourceIndex)];

        foreach (FieldDescriptor f in _fields)
        {
            string? value = f.GetStringValue(item);
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            Field.Store store = f.Store ? Field.Store.YES : Field.Store.NO;

            Field luceneField = f.FieldType switch
            {
                SearchFieldType.Text => new TextField(f.LuceneFieldName, value, store),
                SearchFieldType.Keyword => new StringField(f.LuceneFieldName, value.ToLowerInvariant(), store),
                SearchFieldType.Date => new StringField(f.LuceneFieldName, value, store),
                _ => new TextField(f.LuceneFieldName, value, store)
            };

            doc.Add(luceneField);
        }

        return doc;
    }

    // ---- Document → source index ----
    public static int GetSourceIndex(Document doc)
    {
        string? raw = doc.Get(IdField, CultureInfo.InvariantCulture);

        return raw is not null
            ? int.Parse(raw, CultureInfo.InvariantCulture)
            : -1;
    }
}