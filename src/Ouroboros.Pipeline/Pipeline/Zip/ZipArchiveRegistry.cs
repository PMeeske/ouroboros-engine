namespace Ouroboros.Pipeline.Ingestion.Zip;

internal static class ZipArchiveRegistry
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, ZipArchiveHolder> Map = new(StringComparer.OrdinalIgnoreCase);
    public static ZipArchiveHolder Acquire(string path)
    {
        return Map.AddOrUpdate(path,
            p => new ZipArchiveHolder(p),
            (p, existing) => { existing.AddRef(); return existing; });
    }
    public static void Release(string path)
    {
        if (Map.TryGetValue(path, out ZipArchiveHolder? holder))
        {
            if (holder.ReleaseRef() <= 0)
            {
                holder.Dispose();
                Map.TryRemove(path, out _);
            }
        }
    }
}