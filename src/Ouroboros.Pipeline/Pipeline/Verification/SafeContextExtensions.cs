namespace Ouroboros.Pipeline.Verification;

/// <summary>
/// Extension methods for <see cref="SafeContext"/>.
/// </summary>
public static class SafeContextExtensions
{
    /// <summary>
    /// Converts the context to a MeTTa atom representation.
    /// </summary>
    /// <param name="context">The context to convert.</param>
    /// <returns>The MeTTa atom string.</returns>
    public static string ToMeTTaAtom(this SafeContext context) => context switch
    {
        SafeContext.ReadOnly => "ReadOnly",
        SafeContext.FullAccess => "FullAccess",
        _ => throw new ArgumentOutOfRangeException(nameof(context), context, "Unknown context type"),
    };
}