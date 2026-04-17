// <copyright file="EngineInventory.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.MeTTa.ConformanceScanner.Inventory;

/// <summary>
/// Read-only snapshot of MeTTa engine registrations from the four canonical paths.
/// </summary>
public sealed record EngineInventory(
    IReadOnlyList<RegisteredOperation> Standard,
    IReadOnlyList<RegisteredOperation> ToolsEngine,
    IReadOnlyList<RegisteredOperation> ApplicationLayer,
    IReadOnlyList<RegisteredOperation> LateBound)
{
    /// <summary>
    /// Flattened distinct operation names: standard → tools → application → late-bound.
    /// </summary>
    public IEnumerable<string> AllNames =>
        this.Standard.Concat(this.ToolsEngine).Concat(this.ApplicationLayer).Concat(this.LateBound)
            .Select(static op => op.Name)
            .Distinct(StringComparer.Ordinal);

    /// <summary>
    /// All operations in bucket order (duplicates preserved across buckets).
    /// </summary>
    public IEnumerable<RegisteredOperation> AllOperations =>
        this.Standard.Concat(this.ToolsEngine).Concat(this.ApplicationLayer).Concat(this.LateBound);
}
