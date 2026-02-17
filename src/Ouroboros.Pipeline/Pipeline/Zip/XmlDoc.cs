using System.Xml.Linq;

namespace Ouroboros.Pipeline.Ingestion.Zip;

/// <summary>
/// Wraps an XML document loaded from a zip entry.
/// </summary>
/// <param name="Document">The loaded XML document.</param>
public sealed record XmlDoc(XDocument Document);