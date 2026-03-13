// ==========================================================
// Moral Reasoning Engine
// Multi-framework ethical deliberation with developmental tracking
// ==========================================================

using System.Collections.Concurrent;

namespace Ouroboros.Agent.MetaAI.SelfImprovement;

/// <summary>
/// Ethical framework used for moral evaluation.
/// </summary>
public enum MoralFramework
{
    /// <summary>Duty-based ethics — actions are right or wrong in themselves.</summary>
    Deontological,

    /// <summary>Consequence-based ethics — maximize overall well-being.</summary>
    Utilitarian,

    /// <summary>Character-based ethics — does the action exemplify virtues?</summary>
    VirtueEthics,

    /// <summary>Relationship-based ethics — preserve care and connection.</summary>
    CareEthics
}

/// <summary>
/// Kohlberg-inspired moral development level.
/// </summary>
public enum MoralDevelopmentLevel
{
    /// <summary>Self-interest and punishment avoidance.</summary>
    PreConventional,

    /// <summary>Social norms and rule-following.</summary>
    Conventional,

    /// <summary>Universal principles and autonomous moral reasoning.</summary>
    PostConventional
}

/// <summary>
/// A single framework's verdict on a moral question.
/// </summary>
/// <param name="Framework">The framework used.</param>
/// <param name="IsPermissible">Whether the action is permissible under this framework.</param>
/// <param name="Confidence">Confidence in the verdict (0–1).</param>
/// <param name="Reasoning">Explanation of the verdict.</param>
public sealed record FrameworkVerdict(
    MoralFramework Framework,
    bool IsPermissible,
    double Confidence,
    string Reasoning);

/// <summary>
/// Result of a moral evaluation across all frameworks.
/// </summary>
/// <param name="Action">The action being evaluated.</param>
/// <param name="Verdicts">Per-framework verdicts.</param>
/// <param name="SynthesizedVerdict">Overall verdict from majority vote.</param>
/// <param name="OverallConfidence">Average confidence across frameworks.</param>
public sealed record MoralJudgment(
    string Action,
    IReadOnlyList<FrameworkVerdict> Verdicts,
    bool SynthesizedVerdict,
    double OverallConfidence);

/// <summary>
/// Result of moral deliberation on a dilemma.
/// </summary>
/// <param name="Dilemma">The dilemma being deliberated.</param>
/// <param name="Judgment">The moral judgment.</param>
/// <param name="DevelopmentLevel">Current moral development level.</param>
/// <param name="ReasoningSophistication">Sophistication score (0–1).</param>
public sealed record MoralDeliberation(
    string Dilemma,
    MoralJudgment Judgment,
    MoralDevelopmentLevel DevelopmentLevel,
    double ReasoningSophistication);

/// <summary>
/// Implements multi-framework moral reasoning combining deontological,
/// utilitarian, virtue ethics, and care ethics perspectives. Synthesizes
/// verdicts via majority vote and tracks moral development level based
/// on reasoning sophistication over time.
/// </summary>
public sealed class MoralReasoningEngine
{
    private static readonly string[] DutyViolationKeywords =
        ["harm", "lie", "steal", "break promise", "deceive", "betray", "cheat", "exploit"];

    private static readonly string[] VirtueKeywords =
        ["courage", "honesty", "compassion", "justice", "temperance", "integrity", "wisdom"];

    private static readonly string[] RelationshipDamageKeywords =
        ["abandon", "neglect", "isolate", "ignore", "dismiss", "reject"];

    private int _totalDeliberations;
    private double _cumulativeSophistication;
    private readonly ConcurrentBag<MoralJudgment> _judgmentHistory = new();

    /// <summary>
    /// Evaluates an action against all four moral frameworks.
    /// </summary>
    /// <param name="action">The action to evaluate.</param>
    /// <param name="context">Situational context.</param>
    /// <param name="stakeholders">Affected parties.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="MoralJudgment"/> with per-framework verdicts.</returns>
    public Task<MoralJudgment> EvaluateAsync(
        string action,
        string context,
        IReadOnlyList<string> stakeholders,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(stakeholders);
        ct.ThrowIfCancellationRequested();

        string combined = $"{action} {context}".ToLowerInvariant();
        var verdicts = new List<FrameworkVerdict>
        {
            EvaluateDeontological(action, combined),
            EvaluateUtilitarian(action, combined, stakeholders),
            EvaluateVirtueEthics(action, combined),
            EvaluateCareEthics(action, combined, stakeholders)
        };

        int permissibleCount = verdicts.Count(v => v.IsPermissible);
        bool synthesized = permissibleCount > verdicts.Count / 2;
        double avgConfidence = verdicts.Average(v => v.Confidence);

        var judgment = new MoralJudgment(action, verdicts, synthesized, Math.Round(avgConfidence, 3));
        _judgmentHistory.Add(judgment);

        return Task.FromResult(judgment);
    }

    /// <summary>
    /// Deliberates on a moral dilemma, producing a judgment with development level tracking.
    /// </summary>
    /// <param name="dilemma">Description of the moral dilemma.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="MoralDeliberation"/> with synthesized verdict and development level.</returns>
    public async Task<MoralDeliberation> DeliberateAsync(string dilemma, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dilemma);

        var judgment = await EvaluateAsync(dilemma, dilemma, ["self", "others", "society"], ct).ConfigureAwait(false);

        double sophistication = CalculateSophistication(judgment);
        _totalDeliberations++;
        _cumulativeSophistication += sophistication;

        var level = GetCurrentDevelopmentLevel();

        return new MoralDeliberation(dilemma, judgment, level, Math.Round(sophistication, 3));
    }

    /// <summary>
    /// Returns the current moral development level based on reasoning sophistication.
    /// </summary>
    public MoralDevelopmentLevel GetCurrentDevelopmentLevel()
    {
        if (_totalDeliberations == 0)
            return MoralDevelopmentLevel.PreConventional;

        double avgSophistication = _cumulativeSophistication / _totalDeliberations;

        return avgSophistication switch
        {
            >= 0.7 => MoralDevelopmentLevel.PostConventional,
            >= 0.4 => MoralDevelopmentLevel.Conventional,
            _ => MoralDevelopmentLevel.PreConventional
        };
    }

    /// <summary>
    /// Returns the total number of moral deliberations performed.
    /// </summary>
    public int TotalDeliberations => _totalDeliberations;

    private static FrameworkVerdict EvaluateDeontological(string action, string combined)
    {
        int violations = DutyViolationKeywords.Count(k => combined.Contains(k, StringComparison.OrdinalIgnoreCase));
        bool permissible = violations == 0;
        double confidence = permissible ? 0.8 : Math.Min(0.5 + violations * 0.15, 0.95);

        string reasoning = permissible
            ? "No duty violations detected — action appears permissible"
            : $"Detected {violations} potential duty violation(s): action may conflict with moral duties";

        return new FrameworkVerdict(MoralFramework.Deontological, permissible, confidence, reasoning);
    }

    private static FrameworkVerdict EvaluateUtilitarian(
        string action, string combined, IReadOnlyList<string> stakeholders)
    {
        string[] positiveIndicators = ["benefit", "help", "improve", "protect", "save", "support", "enhance"];
        string[] negativeIndicators = ["harm", "damage", "hurt", "destroy", "worsen", "risk", "endanger"];

        int positive = positiveIndicators.Count(k => combined.Contains(k, StringComparison.OrdinalIgnoreCase));
        int negative = negativeIndicators.Count(k => combined.Contains(k, StringComparison.OrdinalIgnoreCase));

        // More stakeholders amplify both positive and negative impacts
        double stakeholderMultiplier = 1.0 + Math.Log2(Math.Max(stakeholders.Count, 1)) * 0.1;
        double netUtility = (positive - negative) * stakeholderMultiplier;

        bool permissible = netUtility >= 0;
        double confidence = Math.Min(0.5 + Math.Abs(netUtility) * 0.1, 0.95);

        string reasoning = permissible
            ? $"Net utility positive ({positive} benefits vs {negative} harms across {stakeholders.Count} stakeholders)"
            : $"Net utility negative ({negative} harms outweigh {positive} benefits across {stakeholders.Count} stakeholders)";

        return new FrameworkVerdict(MoralFramework.Utilitarian, permissible, confidence, reasoning);
    }

    private static FrameworkVerdict EvaluateVirtueEthics(string action, string combined)
    {
        int virtuesExemplified = VirtueKeywords.Count(k =>
            combined.Contains(k, StringComparison.OrdinalIgnoreCase));
        int vicesDetected = DutyViolationKeywords.Count(k =>
            combined.Contains(k, StringComparison.OrdinalIgnoreCase));

        bool permissible = virtuesExemplified >= vicesDetected;
        double confidence = Math.Min(0.5 + (virtuesExemplified + vicesDetected) * 0.1, 0.95);

        string reasoning = permissible
            ? $"Action exemplifies {virtuesExemplified} virtue(s) — consistent with virtuous character"
            : $"Action reflects {vicesDetected} vice(s) vs {virtuesExemplified} virtue(s) — inconsistent with virtuous character";

        return new FrameworkVerdict(MoralFramework.VirtueEthics, permissible, confidence, reasoning);
    }

    private static FrameworkVerdict EvaluateCareEthics(
        string action, string combined, IReadOnlyList<string> stakeholders)
    {
        int relationalDamage = RelationshipDamageKeywords.Count(k =>
            combined.Contains(k, StringComparison.OrdinalIgnoreCase));
        string[] careIndicators = ["care", "support", "nurture", "listen", "empathy", "connect", "trust"];
        int carePresent = careIndicators.Count(k =>
            combined.Contains(k, StringComparison.OrdinalIgnoreCase));

        bool permissible = carePresent >= relationalDamage;
        double confidence = Math.Min(0.5 + (carePresent + relationalDamage) * 0.1, 0.95);

        string reasoning = permissible
            ? $"Action preserves relationships ({carePresent} care indicators, {relationalDamage} damage indicators)"
            : $"Action may damage relationships ({relationalDamage} damage indicators vs {carePresent} care indicators)";

        return new FrameworkVerdict(MoralFramework.CareEthics, permissible, confidence, reasoning);
    }

    private static double CalculateSophistication(MoralJudgment judgment)
    {
        double score = 0.0;

        // Multiple frameworks engaged increases sophistication
        int frameworksUsed = judgment.Verdicts.Count;
        score += Math.Min(frameworksUsed * 0.15, 0.6);

        // Disagreement between frameworks indicates nuanced reasoning
        bool hasDisagreement = judgment.Verdicts.Any(v => v.IsPermissible) &&
                               judgment.Verdicts.Any(v => !v.IsPermissible);
        if (hasDisagreement)
            score += 0.2;

        // Higher average confidence indicates clearer reasoning
        score += judgment.OverallConfidence * 0.2;

        return Math.Min(score, 1.0);
    }
}
