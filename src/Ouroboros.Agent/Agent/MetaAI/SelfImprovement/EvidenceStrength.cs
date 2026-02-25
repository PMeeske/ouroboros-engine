namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Evidence strength categories based on Jeffreys' scale.
/// </summary>
public enum EvidenceStrength
{
    /// <summary>
    /// Negligible evidence (log10 Bayes factor &lt; 0.5)
    /// </summary>
    Negligible,
    
    /// <summary>
    /// Substantial evidence (log10 Bayes factor 0.5-1.0)
    /// </summary>
    Substantial,
    
    /// <summary>
    /// Strong evidence (log10 Bayes factor 1.0-1.5)
    /// </summary>
    Strong,
    
    /// <summary>
    /// Very strong evidence (log10 Bayes factor 1.5-2.0)
    /// </summary>
    VeryStrong,
    
    /// <summary>
    /// Decisive evidence (log10 Bayes factor &gt; 2.0)
    /// </summary>
    Decisive
}