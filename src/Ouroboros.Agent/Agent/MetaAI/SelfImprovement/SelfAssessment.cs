namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents a self-assessment of agent performance.
/// </summary>
public sealed record SelfAssessment(
    double OverallPerformance,
    double ConfidenceCalibration,
    double SkillAcquisitionRate,
    Dictionary<string, double> CapabilityScores,
    List<string> Strengths,
    List<string> Weaknesses,
    DateTime AssessmentTime,
    string Summary);