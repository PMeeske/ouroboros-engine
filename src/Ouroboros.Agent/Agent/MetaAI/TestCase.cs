namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// A test case for evaluation.
/// </summary>
public sealed record TestCase(
    string Name,
    string Goal,
    Dictionary<string, object>? Context,
    Func<PlanVerificationResult, bool>? CustomValidator);