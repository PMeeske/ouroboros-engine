using System.IO.Compression;

namespace Ouroboros.Pipeline.Ingestion.Zip;

internal sealed class ZipArchiveHolder : IDisposable
{
    public FileStream Stream { get; }
    public ZipArchive Archive { get; }
    private int _refCount;
    public ZipArchiveHolder(string path)
    {
        Stream = File.OpenRead(path);
        Archive = new ZipArchive(Stream, ZipArchiveMode.Read, leaveOpen: false);
        _refCount = 1;
    }
    public void AddRef() => Interlocked.Increment(ref _refCount);
    public int ReleaseRef() => Interlocked.Decrement(ref _refCount);
    public void Dispose()
    {
        try { Archive.Dispose(); } catch { }
        try { Stream.Dispose(); } catch { }
    }
}