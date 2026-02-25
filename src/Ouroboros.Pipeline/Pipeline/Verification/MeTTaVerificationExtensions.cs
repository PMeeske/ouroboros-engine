using Ouroboros.Tools.MeTTa;

namespace Ouroboros.Pipeline.Verification;

/// <summary>
/// Extension methods for integrating MeTTa verification into pipelines.
/// </summary>
public static class MeTTaVerificationExtensions
{
    /// <summary>
    /// Adds MeTTa verification to a plan generation pipeline.
    /// </summary>
    /// <param name="planStep">The step that generates plans.</param>
    /// <param name="engine">The MeTTa engine for verification.</param>
    /// <param name="context">The security context.</param>
    /// <typeparam name="TInput">The input type.</typeparam>
    /// <returns>A composed step that generates and verifies plans.</returns>
    public static Step<TInput, Result<Plan, SecurityException>> WithVerification<TInput>(
        this Step<TInput, Plan> planStep,
        IMeTTaEngine engine,
        SafeContext context = SafeContext.ReadOnly)
    {
        MeTTaVerificationStep verifier = new(engine, context);
        return async input =>
        {
            Plan plan = await planStep(input);
            return await verifier.VerifyAsync(plan);
        };
    }
}