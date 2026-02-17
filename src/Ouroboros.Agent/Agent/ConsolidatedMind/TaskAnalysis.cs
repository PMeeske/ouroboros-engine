namespace Ouroboros.Agent.ConsolidatedMind;

/// <summary>
/// Result of task analysis for routing decisions.
/// </summary>
/// <param name="PrimaryRole">The primary role best suited for this task.</param>
/// <param name="SecondaryRoles">Secondary roles that might assist.</param>
/// <param name="RequiredCapabilities">Capabilities required to complete the task.</param>
/// <param name="EstimatedComplexity">Complexity score from 0 (trivial) to 1 (highly complex).</param>
/// <param name="RequiresThinking">Whether extended thinking mode is recommended.</param>
/// <param name="RequiresVerification">Whether verification step is recommended.</param>
/// <param name="Confidence">Confidence in this analysis (0 to 1).</param>
public sealed record TaskAnalysis(
    SpecializedRole PrimaryRole,
    SpecializedRole[] SecondaryRoles,
    string[] RequiredCapabilities,
    double EstimatedComplexity,
    bool RequiresThinking,
    bool RequiresVerification,
    double Confidence);