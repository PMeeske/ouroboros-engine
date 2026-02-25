namespace Ouroboros.Pipeline.Ingestion;

internal sealed class DirectoryIngestionCache
{
    private readonly string _path;
    private readonly Dictionary<string, string> _hashes = new(StringComparer.OrdinalIgnoreCase);
    private bool _dirty;
    public DirectoryIngestionCache(string path)
    {
        _path = Path.GetFullPath(path);
        try
        {
            if (File.Exists(_path))
            {
                string json = File.ReadAllText(_path);
                Dictionary<string, string>? loaded = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (loaded is not null)
                {
                    foreach (KeyValuePair<string, string> kv in loaded) _hashes[kv.Key] = kv.Value;
                }
            }
        }
        catch { /* ignore cache load issues */ }
    }

    public bool IsUnchanged(string file)
    {
        try
        {
            string h = ComputeHash(file);
            if (_hashes.TryGetValue(file, out string? existing) && existing == h) return true;
            return false;
        }
        catch { return false; }
    }

    public void UpdateHash(string file)
    {
        try
        {
            string h = ComputeHash(file);
            _hashes[file] = h;
            _dirty = true;
        }
        catch { }
    }

    public void Persist()
    {
        if (!_dirty) return;
        try
        {
            string json = System.Text.Json.JsonSerializer.Serialize(_hashes);
            File.WriteAllText(_path, json);
            _dirty = false;
        }
        catch { }
    }

    private static string ComputeHash(string file)
    {
        using System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create();
        using FileStream fs = File.OpenRead(file);
        byte[] hash = sha.ComputeHash(fs);
        return Convert.ToHexString(hash);
    }
}