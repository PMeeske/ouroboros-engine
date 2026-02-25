namespace Ouroboros.Pipeline.Ingestion.Zip;

/// <summary>
/// Provides a thread-safe cache for storing and retrieving deferred zip text content.
/// </summary>
public static class DeferredZipTextCache
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> Map = new();

    /// <summary>
    /// Stores text content associated with an identifier.
    /// </summary>
    /// <param name="id">The unique identifier for the cached text.</param>
    /// <param name="text">The text content to store.</param>
    public static void Store(string id, string text) => Map[id] = text;

    /// <summary>
    /// Attempts to retrieve and remove text content associated with an identifier.
    /// </summary>
    /// <param name="id">The unique identifier for the cached text.</param>
    /// <param name="text">When this method returns, contains the retrieved text if found; otherwise, an empty string.</param>
    /// <returns>True if the text was found and removed; otherwise, false.</returns>
    public static bool TryTake(string id, out string text)
    {
        if (Map.TryRemove(id, out text!)) return true;
        text = string.Empty; return false;
    }

    /// <summary>
    /// Attempts to retrieve text content associated with an identifier without removing it.
    /// </summary>
    /// <param name="id">The unique identifier for the cached text.</param>
    /// <param name="text">When this method returns, contains the retrieved text if found; otherwise, null.</param>
    /// <returns>True if the text was found; otherwise, false.</returns>
    public static bool TryPeek(string id, out string text) => Map.TryGetValue(id, out text!);
}