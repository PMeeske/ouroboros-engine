// <copyright file="SimpleModel.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Domain.MetaLearning;

namespace Ouroboros.Agent.MetaLearning;

/// <summary>
/// Simple implementation of IModel for testing and demonstration.
/// Wraps a basic prediction function with parameter management.
/// </summary>
public class SimpleModel : IModel
{
    private Dictionary<string, object> _parameters;
    private readonly Func<string, Dictionary<string, object>, string> _predictionFunc;

    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleModel"/> class.
    /// </summary>
    /// <param name="predictionFunc">Function that makes predictions given input and parameters.</param>
    /// <param name="initialParameters">Initial model parameters.</param>
    public SimpleModel(
        Func<string, Dictionary<string, object>, string> predictionFunc,
        Dictionary<string, object>? initialParameters = null)
    {
        ArgumentNullException.ThrowIfNull(predictionFunc);
        _predictionFunc = predictionFunc;
        _parameters = initialParameters ?? new Dictionary<string, object>();
    }

    /// <inheritdoc/>
    public Task<string> PredictAsync(string input, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var result = _predictionFunc(input, _parameters);
        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task<IModel> CloneAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var clonedParams = new Dictionary<string, object>(_parameters);
        IModel cloned = new SimpleModel(_predictionFunc, clonedParams);
        return Task.FromResult(cloned);
    }

    /// <inheritdoc/>
    public Task UpdateParametersAsync(
        Dictionary<string, object> gradients,
        double learningRate,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        foreach (var (key, gradient) in gradients)
        {
            if (_parameters.ContainsKey(key))
            {
                // Simple gradient descent: param = param - learningRate * gradient
                if (_parameters[key] is double paramValue && gradient is double gradValue)
                {
                    _parameters[key] = paramValue - (learningRate * gradValue);
                }
                else if (_parameters[key] is double[] paramArray && gradient is double[] gradArray)
                {
                    var updated = new double[paramArray.Length];
                    for (var i = 0; i < paramArray.Length; i++)
                    {
                        updated[i] = paramArray[i] - (learningRate * gradArray[i]);
                    }

                    _parameters[key] = updated;
                }
            }
            else
            {
                _parameters[key] = gradient;
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<Dictionary<string, object>> GetParametersAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(new Dictionary<string, object>(_parameters));
    }

    /// <inheritdoc/>
    public Task SetParametersAsync(Dictionary<string, object> parameters, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _parameters = new Dictionary<string, object>(parameters);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<Dictionary<string, object>> ComputeGradientsAsync(
        List<Example> examples,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // Simple mock gradient computation
        // In a real implementation, this would compute actual gradients via backpropagation
        var gradients = new Dictionary<string, object>();

        foreach (var (key, value) in _parameters)
        {
            if (value is double)
            {
                // Mock gradient: random small value
                gradients[key] = Random.Shared.NextDouble() * 0.01;
            }
            else if (value is double[] arrayValue)
            {
                var gradArray = new double[arrayValue.Length];
                for (var i = 0; i < arrayValue.Length; i++)
                {
                    gradArray[i] = Random.Shared.NextDouble() * 0.01;
                }

                gradients[key] = gradArray;
            }
        }

        return Task.FromResult(gradients);
    }
}
