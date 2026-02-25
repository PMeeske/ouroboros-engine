namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Extension methods for Option type.
/// </summary>
internal static class OptionExtensions
{
    /// <summary>
    /// Converts a nullable value to an Option.
    /// </summary>
    public static Option<T> ToOption<T>(this T? value)
        where T : class
    {
        return value != null ? Option<T>.Some(value) : Option<T>.None();
    }
}