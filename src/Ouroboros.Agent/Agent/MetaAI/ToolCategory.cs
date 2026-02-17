namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Categories of tools for dynamic selection.
/// </summary>
public enum ToolCategory
{
    /// <summary>General purpose tools.</summary>
    General,

    /// <summary>Code-related tools (compilation, formatting, analysis).</summary>
    Code,

    /// <summary>File system operations.</summary>
    FileSystem,

    /// <summary>Web and API tools.</summary>
    Web,

    /// <summary>Knowledge and search tools.</summary>
    Knowledge,

    /// <summary>Analysis and metrics tools.</summary>
    Analysis,

    /// <summary>Validation and testing tools.</summary>
    Validation,

    /// <summary>Text processing tools.</summary>
    Text,

    /// <summary>Reasoning and logic tools.</summary>
    Reasoning,

    /// <summary>Creative generation tools.</summary>
    Creative,

    /// <summary>Utility and helper tools.</summary>
    Utility
}