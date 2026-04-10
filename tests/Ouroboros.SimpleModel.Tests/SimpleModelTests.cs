// <copyright file="SimpleModelTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.MetaLearning;
using Ouroboros.Domain.MetaLearning;
using Xunit;

namespace Ouroboros.MetaLearning.Tests;

public class SimpleModelTests
{
    /// <summary>
    /// Prediction function: output = (input * weight + offset) as a numeric string.
    /// </summary>
    private static string LinearPrediction(string input, Dictionary<string, object> parameters)
    {
        var x = double.Parse(input);
        var weight = (double)parameters["weight"];
        var offset = (double)parameters["offset"];
        return (x * weight + offset).ToString();
    }

    [Fact]
    public async Task ComputeGradients_ReturnsNonRandomGradients()
    {
        // Arrange
        var model = new SimpleModel(
            LinearPrediction,
            new Dictionary<string, object>
            {
                ["weight"] = 1.0,
                ["offset"] = 0.0,
            });

        var examples = new List<Example>
        {
            Example.Create("2.0", "5.0"),
            Example.Create("3.0", "7.0"),
        };

        // Act
        var gradients = await model.ComputeGradientsAsync(examples);

        // Assert
        gradients.Should().ContainKey("weight");
        gradients.Should().ContainKey("offset");

        var weightGrad = (double)gradients["weight"];
        var offsetGrad = (double)gradients["offset"];

        // Gradients should be non-zero (model is wrong: weight=1 but should be ~2)
        weightGrad.Should().NotBe(0.0);
        offsetGrad.Should().NotBe(0.0);

        // Gradients should be deterministic (not random) — call again and compare
        var gradients2 = await model.ComputeGradientsAsync(examples);
        var weightGrad2 = (double)gradients2["weight"];
        var offsetGrad2 = (double)gradients2["offset"];

        weightGrad.Should().Be(weightGrad2);
        offsetGrad.Should().Be(offsetGrad2);
    }

    [Fact]
    public async Task ComputeGradients_IsDeterministic_SameInputSameOutput()
    {
        // Arrange
        var model = new SimpleModel(
            LinearPrediction,
            new Dictionary<string, object>
            {
                ["weight"] = 2.0,
                ["offset"] = 1.0,
            });

        var examples = new List<Example>
        {
            Example.Create("1.0", "3.0"),
            Example.Create("2.0", "5.0"),
        };

        // Act
        var gradients1 = await model.ComputeGradientsAsync(examples);
        var gradients2 = await model.ComputeGradientsAsync(examples);

        // Assert — identical gradient values on repeated calls
        foreach (var key in gradients1.Keys)
        {
            gradients2.Should().ContainKey(key);
            if (gradients1[key] is double val1 && gradients2[key] is double val2)
            {
                val2.Should().Be(val1, $"gradient for '{key}' should be deterministic");
            }
            else if (gradients1[key] is double[] arr1 && gradients2[key] is double[] arr2)
            {
                arr2.Should().Equal(arr1, $"gradient array for '{key}' should be deterministic");
            }
        }
    }

    [Fact]
    public async Task ComputeGradients_ReflectsParameterSensitivity()
    {
        // Arrange — "a" directly controls output magnitude, "b" is unused by prediction
        static string Prediction(string input, Dictionary<string, object> parameters)
        {
            var a = (double)parameters["a"];
            var x = double.Parse(input);
            return (x * a).ToString();
        }

        var model = new SimpleModel(
            Prediction,
            new Dictionary<string, object>
            {
                ["a"] = 1.0,
                ["b"] = 0.5,
            });

        var examples = new List<Example>
        {
            Example.Create("2.0", "5.0"), // target output = 5, but 2*1 = 2, so a is wrong
        };

        // Act
        var gradients = await model.ComputeGradientsAsync(examples);

        // Assert — "a" directly affects the output, so its gradient should be larger
        var gradA = Math.Abs((double)gradients["a"]);
        var gradB = Math.Abs((double)gradients["b"]);

        gradA.Should().BeGreaterThan(gradB, "parameter 'a' should have a larger gradient because it directly drives the output error");
    }

    [Fact]
    public async Task UpdateParameters_WithComputedGradients_ReducesLoss()
    {
        // Arrange — model predicts output = input * weight + offset, target is weight=2.5, offset=0
        var model = new SimpleModel(
            LinearPrediction,
            new Dictionary<string, object>
            {
                ["weight"] = 1.0,
                ["offset"] = 0.0,
            });

        var examples = new List<Example>
        {
            Example.Create("2.0", "5.0"),
        };

        // Compute initial loss
        var initialPrediction = await model.PredictAsync("2.0");
        var initialLoss = ComputeSquaredLoss(initialPrediction, "5.0");

        // Act — compute gradients and update parameters with small learning rate
        // (gradients can be large for far-from-optimal parameters, so use small lr)
        var gradients = await model.ComputeGradientsAsync(examples);
        await model.UpdateParametersAsync(gradients, learningRate: 0.001);

        // Compute loss after update
        var updatedPrediction = await model.PredictAsync("2.0");
        var updatedLoss = ComputeSquaredLoss(updatedPrediction, "5.0");

        // Assert — loss should decrease after one gradient step
        updatedLoss.Should().BeLessThan(initialLoss, "loss should decrease after applying computed gradients");
    }

    [Fact]
    public async Task ComputeGradients_ArrayParameters_ElementWiseGradients()
    {
        // Arrange — prediction uses sum of array elements as a weight
        static string ArrayPrediction(string input, Dictionary<string, object> parameters)
        {
            var x = double.Parse(input);
            var weights = (double[])parameters["weights"];
            var bias = (double)parameters["bias"];
            var weightedSum = weights.Sum() * x + bias;
            return weightedSum.ToString();
        }

        var model = new SimpleModel(
            ArrayPrediction,
            new Dictionary<string, object>
            {
                ["weights"] = new double[] { 1.0, 2.0, 3.0 },
                ["bias"] = 0.0,
            });

        var examples = new List<Example>
        {
            Example.Create("1.0", "10.0"), // target: sum=6, so 6*1 = 6, target=10
        };

        // Act
        var gradients = await model.ComputeGradientsAsync(examples);

        // Assert
        gradients.Should().ContainKey("weights");
        gradients.Should().ContainKey("bias");

        var weightGrads = (double[])gradients["weights"];
        weightGrads.Should().HaveCount(3, "gradient array should match parameter array length");

        // Each element should have a computed gradient — they should be finite non-zero values
        foreach (var g in weightGrads)
        {
            g.Should().NotBe(double.NaN);
            g.Should().NotBe(double.PositiveInfinity);
            g.Should().NotBe(double.NegativeInfinity);
        }

        // Verify not all the same (would indicate a bug in element-wise computation)
        // Note: for sum-based prediction, all gradients may legitimately be equal
        // So we just verify they are well-formed finite-difference values
        weightGrads[0].Should().NotBe(0.0, "gradient for weight element should be non-zero since prediction is wrong");
    }

    /// <summary>
    /// Helper to compute squared loss between two numeric strings.
    /// </summary>
    private static double ComputeSquaredLoss(string predicted, string expected)
    {
        var predNum = double.Parse(predicted);
        var expNum = double.Parse(expected);
        var diff = predNum - expNum;
        return diff * diff;
    }
}