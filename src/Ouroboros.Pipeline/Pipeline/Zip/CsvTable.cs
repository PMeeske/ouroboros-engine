namespace Ouroboros.Pipeline.Ingestion.Zip;

/// <summary>
/// Represents a parsed CSV table with header and data rows.
/// </summary>
/// <param name="Header">The header row containing column names.</param>
/// <param name="Rows">The data rows of the table.</param>
public sealed record CsvTable(string[] Header, List<string[]> Rows);