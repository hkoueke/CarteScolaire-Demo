using CarteScolaire.Data.Queries;
using CarteScolaire.DataImpl.FuzzySearch.Strategies;
using Lucene.Net.Analysis;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;

namespace CarteScolaire.DataImpl.FuzzySearch;

/// <summary>
/// Generic, in-memory Lucene.NET search service.
///
/// Lifecycle:
/// 1. Create once and reuse (thread-safe reads after <see cref="Build"/>).
/// 2. Call <see cref="Build"/> whenever the source collection changes.
/// 3. Call <see cref="Search"/> for each user query — O(log N) with Lucene.
/// 4. Dispose when no longer needed.
///
/// Type parameter <typeparamref name="T"/> must have at least one property
/// decorated with <see cref="SearchFieldAttribute"/>.
/// </summary>
internal sealed class FuzzySearchService<T> : IDisposable
{
    private const LuceneVersion LuceneVer = LuceneVersion.LUCENE_48;

    private readonly DocumentMapper<T> _mapper = new();
    private readonly Analyzer _analyzer = AnalyzerProvider.BuildForType<T>(LuceneVer);

    // Strategies are stateless — cache them once instead of re-allocating on every Search() call.
    private readonly QueryStrategyBase<T>[] _strategies;

    // Mutable index state — protected by _lock during rebuild, read-only during search.
    private readonly ReaderWriterLockSlim _lock = new();

    private RAMDirectory? _directory;
    private DirectoryReader? _reader;
    private IndexSearcher? _searcher;
    private IReadOnlyList<T> _source = [];

    // Single field tracks disposal state. Interlocked.Exchange provides a full
    // memory barrier, making a separate volatile bool redundant and error-prone.
    private int _disposedInt;
    private bool IsDisposed => _disposedInt == 1;

    public FuzzySearchService()
    {
        _strategies = [
            new StringQueryStrategy<T>(_analyzer),
            new DateOnlyQueryStrategy<T>(),
            new EnumQueryStrategy<T>()
        ];
    }

    /// <summary>
    /// Indexes all items in <paramref name="items"/>.
    /// Safe to call multiple times — rebuilds the index from scratch each time.
    /// </summary>
    public void Build(IEnumerable<T> items)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        ArgumentNullException.ThrowIfNull(items);

        IReadOnlyList<T> list = items as IReadOnlyList<T> ?? items.ToList();

        RAMDirectory newDir = new();
        IndexWriterConfig config = new(LuceneVer, _analyzer)
        {
            OpenMode = OpenMode.CREATE,
            RAMBufferSizeMB = 256d
        };

        using IndexWriter writer = new(newDir, config);
        int count = list.Count;

        for (int i = 0; i < count; i++)
        {
            writer.AddDocument(_mapper.ToDocument(list[i], i));
        }

        writer.Flush(triggerMerge: false, applyAllDeletes: false);
        writer.Commit();

        DirectoryReader newReader = DirectoryReader.Open(newDir);
        IndexSearcher newSearcher = new(newReader);

        // Swap atomically under write lock.
        _lock.EnterWriteLock();
        try
        {
            DisposeIndex();
            _directory = newDir;
            _reader = newReader;
            _searcher = newSearcher;
            _source = list;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Executes a structured search against the current index.
    /// </summary>
    /// <returns>Scored results ordered by relevance, capped at <see cref="SearchQuery.MaxResults"/>.</returns>
    public IReadOnlyList<SearchResult<T>> Search(SearchQuery query)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        ArgumentNullException.ThrowIfNull(query);

        IndexSearcher searcher;
        IReadOnlyList<T> source;

        _lock.EnterReadLock();
        try
        {
            searcher = _searcher ?? throw new InvalidOperationException($"Index not built. Call {nameof(Build)} before {nameof(Search)}.");
            source = _source;
        }
        finally
        {
            _lock.ExitReadLock();
        }

        Query luceneQuery = QueryBuilder<T>.Build(query, _strategies);
        TopDocs topDocs = searcher.Search(luceneQuery, query.MaxResults);

        if (topDocs.TotalHits == 0)
        {
            return [];
        }

        float topScore = topDocs.ScoreDocs[0].Score;
        ScoreDoc[] scoreDocs = topDocs.ScoreDocs;
        List<SearchResult<T>> results = new(scoreDocs.Length);

        for (int rank = 0; rank < scoreDocs.Length; rank++)
        {
            ScoreDoc scoreDoc = scoreDocs[rank];
            Document doc = searcher.Doc(scoreDoc.Doc);
            int idx = DocumentMapper<T>.GetSourceIndex(doc);

            if (idx < 0 || idx >= source.Count)
            {
                continue;
            }

            float normalised = topScore > 0f ? scoreDoc.Score / topScore : 0f;
            results.Add(new SearchResult<T>(item: source[idx], score: normalised, rank: rank + 1));
        }

        return results.AsReadOnly();
    }

    public void Dispose()
    {
        // Interlocked.Exchange guarantees only one thread proceeds with disposal.
        // Its full memory barrier makes a separate volatile bool field unnecessary.
        if (Interlocked.Exchange(ref _disposedInt, 1) != 0)
        {
            return;
        }

        _lock.EnterWriteLock();
        try
        {
            DisposeIndex();
            _analyzer.Dispose();
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        _lock.Dispose();
    }

    private void DisposeIndex()
    {
        _reader?.Dispose();
        _directory?.Dispose();
        _reader = null;
        _directory = null;
        _searcher = null;
    }
}