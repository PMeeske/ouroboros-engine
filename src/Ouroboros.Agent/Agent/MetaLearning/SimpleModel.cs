// <copyright file="SimpleModel.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Domain.MetaLearning;

namespace Ouroboros.Agent.MetaLearning;

/// <summary>
/// Simple implementation of IModel for testing and demonstration.
/// Wraps a basic prediction function with parameter management.
/// Uses finite-difference gradient computation instead of random values.
/// </summary>
public class SimpleModel : IModel
{
    private Dictionary<string, object> _parameters;
    private readonly Func<string, Dictionary<string, object>, string> _predictionFunc;
    private readonly double _delta;

    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleModel"/> class.
    /// </summary>
    /// <param name="predictionFunc">Function that makes predictions given input and parameters.</param>
    /// <param name="initialParameters">Initial model parameters.</param>
    /// <param name="delta">Perturbation size for finite-difference gradient computation.</param>
    public SimpleModel(
        Func<string, Dictionary<string, object>, string> predictionFunc,
        Dictionary<string, object>? initialParameters = null,
        double delta = 1e-5)
    {
        ArgumentNullException.ThrowIfNull(predictionFunc);
        _predictionFunc = predictionFunc;
        _parameters = initialParameters ?? new Dictionary<string, object>();
        _delta = delta;
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

        var gradients = new Dictionary<string, object>();
        var baseLoss = ComputeLossForExamples(examples);

        foreach (var (key, value) in _parameters)
        {
            if (value is double scalarValue)
            {
                gradients[key] = ComputeScalarGradient(key, scalarValue, examples, baseLoss);
            }
            else if (value is double[] arrayValue)
            {
                gradients[key] = ComputeArrayGradient(key, arrayValue, examples, baseLoss);
            }

            // Skip parameters that are not double or double[]
        }

        return Task.FromResult(gradients);
    }

    /// <summary>
    /// Computes average loss over examples by comparing predictions to expected outputs.
    /// </summary>
    /// <param name="examples">Input-output pairs to evaluate.</param>
    /// <returns>Average loss value.</returns>
    private float ComputeLossForExamples(List<Example> examples)
    {
        if (examples.Count == 0)
        {
            return 0f;
        }

        var totalLoss = 0.0;
        foreach (var example in examples)
        {
            var predicted = _predictionFunc(example.Input, _parameters);
            totalLoss += ComputeSingleLoss(predicted, example.Output);
        }

        return (float)(totalLoss / examples.Count);
    }

    /// <summary>
    /// Computes loss between a predicted and expected output string.
    /// Uses squared error for numeric values, normalized character distance for strings.
    /// </summary>
    private static double ComputeSingleLoss(string predicted, string expected)
    {
        if (double.TryParse(predicted, out var predNum) && double.TryParse(expected, out var expNum))
        {
            var diff = predNum - expNum;
            return diff * diff;
        }

        // String-based loss: normalized character distance
        var maxLen = Math.Max(predicted.Length, expected.Length);
        if (maxLen == 0)
        {
            return 0.0;
        }

        var mismatches = 0;
        for (var i = 0; i < maxLen; i++)
        {
            var predChar = i < predicted.Length ? predicted[i] : '\0';
            var expChar = i < expected.Length ? expected[i] : '\0';
            if (predChar != expChar)
            {
                mismatches++;
            }
        }

        return (double)mismatches / maxLen;
    }

    /// <summary>
    /// Computes finite-difference gradient for a scalar parameter using central differences.
    /// </summary>
    private double ComputeScalarGradient(string key, double currentValue, List<Example> examples, float baseLoss)
    {
        // Perturb forward
        _parameters[key] = currentValue + _delta;
        var lossPlus = ComputeLossForExamples(examples);

        // Perturb backward
        _parameters[key] = currentValue - _delta;
        var lossMinus = ComputeLossForExamples(examples);

        // Restore original value
        _parameters[key] = currentValue;

        // Central difference gradient
        return (lossPlus - lossMinus) / (2.0 * _delta);
    }

    /// <summary>
    /// Computes finite-difference gradient for an array parameter, element-wise.
    /// </summary>
    private double[] ComputeArrayGradient(string key, double[] currentArray, List<Example> examples, float baseLoss)
    {
        var gradArray = new double[currentArray.Length];

        for (var i = 0; i < currentArray.Length; i++)
        {
            var original = currentArray[i];

            // Perturb element forward
            currentArray[i] = original + _delta;
            _parameters[key] = currentArray;
            var lossPlus = ComputeLossForExamples(examples);

            // Perturb element backward
            currentArray[i] = original - _delta;
            _parameters[key] = currentArray;
            var lossMinus = ComputeLossForExamples(examples);

            // Restore original element value
            currentArray[i] = original;

            // Central difference gradient for this element
            gradArray[i] = (lossPlus - lossMinus) / (2.0 * _delta);
        }

        // Restore original array reference
        _parameters[key] = currentArray;

        return gradArray;
    }
}
