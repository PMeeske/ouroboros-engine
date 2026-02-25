namespace Ouroboros.Pipeline.Verification;

/// <summary>
/// Represents a file system action.
/// </summary>
/// <param name="Operation">The operation type (e.g., "read", "write", "delete").</param>
/// <param name="Path">The target path for the operation.</param>
public sealed record FileSystemAction(string Operation, string? Path = null) : PlanAction
{
    /// <inheritdoc/>
    public override string ToMeTTaAtom() => $"(FileSystemAction \"{Operation}\")";
}