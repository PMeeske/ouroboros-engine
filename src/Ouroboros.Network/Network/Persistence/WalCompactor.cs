// <copyright file="WalCompactor.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Abstractions;

namespace Ouroboros.Network.Persistence;

/// <summary>
/// Utility for compacting Write-Ahead Log files.
/// Prevents unbounded WAL growth by rebuilding the DAG and writing a new compacted WAL.
/// </summary>
public static class WalCompactor
{
    /// <summary>
    /// Compacts a WAL file by replaying all entries, rebuilding the DAG,
    /// and writing a new compacted WAL containing only the final state.
    /// </summary>
    /// <param name="walPath">The path to the WAL file to compact.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result indicating success or failure with an error message.</returns>
    public static async Task<Result<Unit, string>> CompactAsync(string walPath, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(walPath))
        {
            return Result<Unit, string>.Failure("WAL path cannot be null or empty");
        }

        if (!File.Exists(walPath))
        {
            return Result<Unit, string>.Failure($"WAL file not found: {walPath}");
        }

        try
        {
            // Step 1: Restore the DAG from the existing WAL
            var sourcePersistence = new FileWalPersistence(walPath);
            var restoreResult = await PersistentMerkleDag.RestoreAsync(sourcePersistence, ct).ConfigureAwait(false);

            if (restoreResult.IsFailure)
            {
                await sourcePersistence.DisposeAsync().ConfigureAwait(false);
                return Result<Unit, string>.Failure($"Failed to restore DAG for compaction: {restoreResult.Error}");
            }

            var restoredDag = restoreResult.Value;

            // Step 2: Create a temporary WAL for the compacted data
            var tempWalPath = walPath + ".compact.tmp";
            try
            {
                var targetPersistence = new FileWalPersistence(tempWalPath);

                // Step 3: Write all nodes in topological order
                var sortResult = restoredDag.TopologicalSort();
                if (sortResult.IsFailure)
                {
                    await targetPersistence.DisposeAsync().ConfigureAwait(false);
                    await restoredDag.DisposeAsync().ConfigureAwait(false);
                    return Result<Unit, string>.Failure($"Topological sort failed: {sortResult.Error}");
                }

                foreach (var node in sortResult.Value)
                {
                    await targetPersistence.AppendNodeAsync(node, ct).ConfigureAwait(false);
                }

                // Step 4: Write all edges
                foreach (var edge in restoredDag.Edges.Values)
                {
                    await targetPersistence.AppendEdgeAsync(edge, ct).ConfigureAwait(false);
                }

                await targetPersistence.FlushAsync(ct).ConfigureAwait(false);

                // Step 5: Dispose both persistence layers before file operations
                await targetPersistence.DisposeAsync().ConfigureAwait(false);
                await restoredDag.DisposeAsync().ConfigureAwait(false);

                // Step 6: Replace the original WAL with the compacted version
                // WARNING: These file operations are not atomic. If the process crashes between
                // File.Move calls, the original WAL may be lost. In production, consider using
                // platform-specific atomic rename operations or a transactional file system.
                var backupPath = walPath + ".backup";
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }

                File.Move(walPath, backupPath);
                File.Move(tempWalPath, walPath);

                // Optionally delete the backup
                File.Delete(backupPath);

                return Result<Unit, string>.Success(Unit.Value);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                // Clean up temp file if it exists
                if (File.Exists(tempWalPath))
                {
                    try
                    {
                        File.Delete(tempWalPath);
                    }
                    catch (IOException)
                    {
                        // Best-effort cleanup - file may be locked by another process
                    }
                }

                return Result<Unit, string>.Failure($"Compaction failed: {ex.Message}");
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return Result<Unit, string>.Failure($"Compaction failed: {ex.Message}");
        }
    }
}
