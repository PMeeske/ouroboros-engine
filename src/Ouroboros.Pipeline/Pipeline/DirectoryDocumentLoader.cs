using LangChain.DocumentLoaders;

namespace Ouroboros.Pipeline.Ingestion;

/// <summary>
/// Directory-aware document loader that enumerates all files under a directory (optionally recursively)
/// and delegates loading of each file to the underlying single-file loader provided via generic type parameter.
/// This works around loaders that only accept a single file DataSource.
/// </summary>
/// <typeparam name="TInner">Concrete file loader implementing IDocumentLoader for single files.</typeparam>
public sealed class DirectoryDocumentLoader<TInner> : IDocumentLoader where TInner : IDocumentLoader, new()
{
    private readonly bool _recursive;
    private readonly string[] _fileGlobs;
    private readonly HashSet<string>? _allowedExtensions;
    private readonly HashSet<string>? _excludeDirs;
    private readonly long? _maxFileBytes;
    private readonly bool _useCache;
    private readonly DirectoryIngestionCache? _cache;

    // internal hook to pass stats object without altering interface signature
    private DirectoryIngestionStats? _optionsStats;

    public DirectoryDocumentLoader(bool recursive = true, params string[] fileGlobs)
        : this(new DirectoryIngestionOptions { Recursive = recursive, Patterns = fileGlobs }) { }

    public DirectoryDocumentLoader(DirectoryIngestionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _recursive = options.Recursive;
        _fileGlobs = (options.Patterns is { Length: > 0 }) ? options.Patterns : ["*"];
        _allowedExtensions = options.Extensions?.Length > 0 ? [.. options.Extensions.Select(e => e.StartsWith('.') ? e.ToLowerInvariant() : "." + e.ToLowerInvariant())] : null;
        _excludeDirs = options.ExcludeDirectories?.Length > 0 ? [.. options.ExcludeDirectories.Select(d => d.ToLowerInvariant())] : null;
        _maxFileBytes = options.MaxFileBytes > 0 ? options.MaxFileBytes : null;
        _useCache = !options.DisableCache;
        _cache = _useCache ? new DirectoryIngestionCache(options.CacheFilePath) : null;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<Document>> LoadAsync(
        DataSource source,
        DocumentLoaderSettings? settings = null,
        CancellationToken cancellationToken = default)
    {
        if (source.Value is not string path)
            throw new ArgumentException("DataSource must contain a path string for directory loading");

        if (File.Exists(path))
            return await new TInner().LoadAsync(source, settings, cancellationToken);

        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException($"Directory '{path}' not found");

        List<Document> docs = [];
        bool debug = Environment.GetEnvironmentVariable("MONADIC_DEBUG") == "1";
        DateTime start = DateTime.UtcNow;
        SearchOption dirEnumOption = _recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        DirectoryIngestionStats stats = _optionsStats ?? new DirectoryIngestionStats();

        foreach (string pattern in _fileGlobs)
        {
            foreach (string file in Directory.EnumerateFiles(path, pattern, dirEnumOption))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (ShouldSkipFile(file, path))
                    continue;

                if (_useCache && _cache?.IsUnchanged(file) == true)
                {
                    stats.SkippedUnchanged++;
                    if (debug) System.Diagnostics.Trace.TraceInformation("[ingest] skip unchanged {0}", file);
                    continue;
                }

                await LoadFileIntoDocsAsync(file, path, settings, cancellationToken, docs, stats, debug);

                if (_useCache) _cache?.UpdateHash(file);
                if (debug) System.Diagnostics.Trace.TraceInformation("[ingest] loaded {0}", file);
            }
        }

        if (_useCache) _cache?.Persist();
        stats.Elapsed = DateTime.UtcNow - start;
        if (debug) System.Diagnostics.Trace.TraceInformation("[ingest] summary {0}", stats);

        return docs;
    }

    public void AttachStats(DirectoryIngestionStats stats) => _optionsStats = stats;

    private bool ShouldSkipFile(string file, string rootPath)
    {
        if (_excludeDirs is not null)
        {
            string rel = Path.GetRelativePath(rootPath, Path.GetDirectoryName(file)!);
            string[] parts = rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (parts.Any(p => _excludeDirs.Contains(p.ToLowerInvariant())))
                return true;
        }

        if (_maxFileBytes is not null)
        {
            try
            {
                if (new FileInfo(file).Length > _maxFileBytes)
                    return true;
            }
            catch (IOException) { /* ignore inaccessible files */ }
        }

        if (_allowedExtensions is not null)
        {
            string ext = Path.GetExtension(file).ToLowerInvariant();
            if (!_allowedExtensions.Contains(ext))
                return true;
        }

        return false;
    }

    private async Task LoadFileIntoDocsAsync(
        string file,
        string rootPath,
        DocumentLoaderSettings? settings,
        CancellationToken cancellationToken,
        List<Document> docs,
        DirectoryIngestionStats stats,
        bool debug)
    {
        try
        {
            DataSource fileSource = DataSource.FromPath(file);
            IReadOnlyCollection<Document> loaded = await new TInner().LoadAsync(fileSource, settings, cancellationToken);

            foreach (Document d in loaded)
            {
                docs.Add(BuildDocumentWithMeta(d, rootPath, file));
                stats.FilesLoaded++;
            }

            if (debug) System.Diagnostics.Trace.TraceInformation("[ingest] loaded {0} docs={1}", file, loaded.Count);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            if (debug) System.Diagnostics.Trace.TraceWarning("[ingest] error {0} {1}", file, ex.Message);
            docs.Add(new Document
            {
                PageContent = string.Empty,
                Metadata = new Dictionary<string, object> { ["error"] = ex.Message, ["path"] = file }
            });
            stats.Errors++;
        }
    }

    private static Document BuildDocumentWithMeta(Document source, string rootPath, string filePath)
    {
        IDictionary<string, object> metaBase = source.Metadata ?? new Dictionary<string, object>();
        Dictionary<string, object> meta = new(metaBase)
        {
            ["directoryRoot"] = rootPath,
            ["relativePath"] = Path.GetRelativePath(rootPath, filePath)
        };
        return new Document { PageContent = source.PageContent, Metadata = meta };
    }
}
