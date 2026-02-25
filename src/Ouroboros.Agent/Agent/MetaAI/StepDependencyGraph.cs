namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents a step dependency graph for parallel execution.
/// </summary>
public sealed class StepDependencyGraph
{
    private readonly Dictionary<int, List<int>> _dependencies = new();
    private readonly List<PlanStep> _steps = new();

    public StepDependencyGraph(List<PlanStep> steps)
    {
        _steps = steps;
        AnalyzeDependencies();
    }

    /// <summary>
    /// Groups steps that can be executed in parallel.
    /// </summary>
    public List<List<int>> GetParallelGroups()
    {
        List<List<int>> groups = new List<List<int>>();
        HashSet<int> executed = new HashSet<int>();

        while (executed.Count < _steps.Count)
        {
            List<int> group = new List<int>();

            for (int i = 0; i < _steps.Count; i++)
            {
                if (executed.Contains(i))
                    continue;

                // Can execute if all dependencies are satisfied
                if (!_dependencies.TryGetValue(i, out List<int>? deps) ||
                    deps.All(d => executed.Contains(d)))
                {
                    group.Add(i);
                }
            }

            if (group.Count == 0)
                break; // Circular dependency or error

            groups.Add(group);
            executed.UnionWith(group);
        }

        return groups;
    }

    private void AnalyzeDependencies()
    {
        // Analyze parameter dependencies between steps
        for (int i = 0; i < _steps.Count; i++)
        {
            List<int> deps = new List<int>();
            PlanStep step = _steps[i];

            // Check if this step uses outputs from previous steps
            for (int j = 0; j < i; j++)
            {
                PlanStep prevStep = _steps[j];

                // Check if current step's parameters reference previous step's output
                if (HasDependency(step, prevStep))
                {
                    deps.Add(j);
                }
            }

            if (deps.Any())
            {
                _dependencies[i] = deps;
            }
        }
    }

    private bool HasDependency(PlanStep current, PlanStep previous)
    {
        // Check if current step references previous step's action or expected outcome
        string prevActionRef = $"${previous.Action}";
        string prevOutputRef = $"output_{previous.Action}";

        foreach (object param in current.Parameters.Values)
        {
            string paramStr = param?.ToString() ?? "";
            if (paramStr.Contains(prevActionRef) || paramStr.Contains(prevOutputRef))
            {
                return true;
            }
        }

        return false;
    }
}