using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using CarteScolaire.Data.Queries;

namespace CarteScolaire.DataImpl.FuzzySearch;

/// <summary>
/// Reads and caches <see cref="SearchFieldAttribute"/> metadata for a type.
/// Uses a concurrent dictionary so it is safe to call from multiple threads.
/// </summary>
internal sealed class FieldMetadataCache
{
    // ---- singleton ----
    public static readonly FieldMetadataCache Instance = new();
    private FieldMetadataCache() { }

    // ---- cache ----
    private readonly ConcurrentDictionary<Type, IReadOnlyList<FieldDescriptor>> _cache = new();

    public IReadOnlyList<FieldDescriptor> GetFields<T>() => _cache.GetOrAdd(typeof(T), BuildDescriptors);

    // ---- internals ----
    private static IReadOnlyList<FieldDescriptor> BuildDescriptors(Type type)
    {
        List<FieldDescriptor> descriptors = [];
        PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (PropertyInfo prop in properties)
        {
            SearchFieldAttribute? attr = prop.GetCustomAttribute<SearchFieldAttribute>();
            
            if (attr is null)
            {
                continue;
            }

            descriptors.Add(new FieldDescriptor(
                property: prop,
                luceneFieldName: attr.FieldName ?? prop.Name.ToLowerInvariant(),
                fieldType: attr.FieldType,
                boost: attr.Boost,
                store: attr.Store));
        }
        // Sort for consistent order (e.g., deterministic query building)
        descriptors.Sort((a, b) => string.Compare(a.LuceneFieldName, b.LuceneFieldName, StringComparison.Ordinal));
        return descriptors.AsReadOnly();
    }
}

internal sealed class FieldDescriptor(PropertyInfo property, string luceneFieldName, SearchFieldType fieldType, float boost, bool store)
{
    public PropertyInfo Property { get; } = property ?? throw new ArgumentNullException(nameof(property));
    public string LuceneFieldName { get; } = luceneFieldName ?? throw new ArgumentNullException(nameof(luceneFieldName));
    public SearchFieldType FieldType { get; } = fieldType;
    public float Boost { get; } = boost;
    public bool Store { get; } = store;

    /// <summary>Gets the string value for a given object instance.</summary>
    public string? GetStringValue(object? instance)
    {
        if (instance is null)
        {
            return null;
        }

        object? raw = Property.GetValue(instance);

        if (raw is null)
        {
            return null;
        }

        // Normalise dates to yyyyMMdd for Lucene storage / range queries
        if (FieldType == SearchFieldType.Date)
        {
            return raw switch
            {
                DateOnly d => d.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
                DateTime dt => dt.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
                string s => s, // assume caller already formatted
                _ => Convert.ToString(raw, CultureInfo.InvariantCulture)
            };
        }
        return Convert.ToString(raw, CultureInfo.InvariantCulture);
    }
}