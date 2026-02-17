namespace Ouroboros.Pipeline.Ingestion.Zip;

/// <summary>
/// Specifies the type of content found in a zip archive entry.
/// </summary>
public enum ZipContentKind
{
    /// <summary>
    /// CSV (Comma-Separated Values) file content.
    /// </summary>
    Csv,

    /// <summary>
    /// XML (Extensible Markup Language) document content.
    /// </summary>
    Xml,

    /// <summary>
    /// Plain text content.
    /// </summary>
    Text,

    /// <summary>
    /// Binary or unknown content type.
    /// </summary>
    Binary
}