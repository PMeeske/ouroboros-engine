namespace Ouroboros.Pipeline.Planning;

/// <summary>
/// Represents a type in the MeTTa type system.
/// </summary>
/// <param name="Name">The name of the type.</param>
public sealed record MeTTaType(string Name)
{
    /// <summary>
    /// Common input types.
    /// </summary>
    public static readonly MeTTaType Text = new("Text");
    
    /// <summary>
    /// Summary type.
    /// </summary>
    public static readonly MeTTaType Summary = new("Summary");
    
    /// <summary>
    /// Code type.
    /// </summary>
    public static readonly MeTTaType Code = new("Code");
    
    /// <summary>
    /// Test result type.
    /// </summary>
    public static readonly MeTTaType TestResult = new("TestResult");
    
    /// <summary>
    /// Query type.
    /// </summary>
    public static readonly MeTTaType Query = new("Query");
    
    /// <summary>
    /// Answer type.
    /// </summary>
    public static readonly MeTTaType Answer = new("Answer");

    /// <inheritdoc/>
    public override string ToString() => this.Name;
}