namespace Ouroboros.Pipeline.Planning;

/// <summary>
/// Extension methods for integrating MeTTa planning with tool binding.
/// </summary>
public static class ToolBinderExtensions
{
    /// <summary>
    /// Plans and binds a tool chain in one operation.
    /// </summary>
    /// <param name="planner">The MeTTa planner.</param>
    /// <param name="binder">The tool binder.</param>
    /// <param name="startType">The input type.</param>
    /// <param name="endType">The output type.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing the executable pipeline or an error.</returns>
    public static async Task<Result<Step<string, string>, string>> PlanAndBindAsync(
        this MeTTaPlanner planner,
        ToolBinder binder,
        MeTTaType startType,
        MeTTaType endType,
        CancellationToken ct = default)
    {
        Result<ToolChain, string> planResult = await planner.PlanAsync(startType, endType, ct);

        if (planResult.IsFailure)
        {
            return Result<Step<string, string>, string>.Failure(planResult.Error);
        }

        return binder.Bind(planResult.Value);
    }

    /// <summary>
    /// Plans, binds, and executes a tool chain.
    /// </summary>
    /// <param name="planner">The MeTTa planner.</param>
    /// <param name="binder">The tool binder.</param>
    /// <param name="input">The input to process.</param>
    /// <param name="startType">The input type.</param>
    /// <param name="endType">The output type.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing the output or an error.</returns>
    public static async Task<Result<string, string>> PlanBindAndExecuteAsync(
        this MeTTaPlanner planner,
        ToolBinder binder,
        string input,
        MeTTaType startType,
        MeTTaType endType,
        CancellationToken ct = default)
    {
        Result<Step<string, string>, string> bindResult = 
            await planner.PlanAndBindAsync(binder, startType, endType, ct);

        if (bindResult.IsFailure)
        {
            return Result<string, string>.Failure(bindResult.Error);
        }

        try
        {
            string result = await bindResult.Value(input);
            return Result<string, string>.Success(result);
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Execution failed: {ex.Message}");
        }
    }
}