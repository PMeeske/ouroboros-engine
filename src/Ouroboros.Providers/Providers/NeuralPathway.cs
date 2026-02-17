using Polly.CircuitBreaker;

namespace Ouroboros.Providers;

/// <summary>
/// Represents the health and state of a neural pathway (provider connection).
/// </summary>
public sealed class NeuralPathway
{
    public string Name { get; init; } = "";
    public ChatEndpointType EndpointType { get; init; }
    public Ouroboros.Abstractions.Core.IChatCompletionModel Model { get; init; } = null!;
    public LlmCostTracker CostTracker { get; init; } = null!;
    public AsyncCircuitBreakerPolicy CircuitBreaker { get; init; } = null!;

    /// <summary>
    /// The capability tier of this pathway for routing purposes.
    /// </summary>
    public PathwayTier Tier { get; init; } = PathwayTier.CloudLight;

    /// <summary>
    /// Specialized capabilities this pathway excels at.
    /// </summary>
    public HashSet<SubGoalType> Specializations { get; init; } = new();

    // Health metrics
    public int Synapses { get; set; } // Total requests
    public int Activations { get; set; } // Successful requests
    public int Inhibitions { get; set; } // Failed requests
    public DateTime? LastActivation { get; set; }
    public TimeSpan AverageLatency { get; set; }

    // Adaptive weight based on performance
    public double Weight { get; set; } = 1.0;

    public bool IsHealthy => CircuitBreaker.CircuitState != CircuitState.Open;
    public double ActivationRate => Synapses > 0 ? (double)Activations / Synapses : 1.0;

    public void RecordActivation(TimeSpan latency)
    {
        Synapses++;
        Activations++;
        LastActivation = DateTime.UtcNow;

        // Exponential moving average for latency
        AverageLatency = AverageLatency == TimeSpan.Zero
            ? latency
            : TimeSpan.FromMilliseconds(AverageLatency.TotalMilliseconds * 0.8 + latency.TotalMilliseconds * 0.2);

        // Increase weight for reliable pathways
        Weight = Math.Min(2.0, Weight * 1.05);
    }

    public void RecordInhibition()
    {
        Synapses++;
        Inhibitions++;

        // Decrease weight for unreliable pathways
        Weight = Math.Max(0.1, Weight * 0.7);
    }
}