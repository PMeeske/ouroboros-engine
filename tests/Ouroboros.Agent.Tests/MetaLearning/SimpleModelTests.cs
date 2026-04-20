// <copyright file="SimpleModelTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.MetaLearning;
using Ouroboros.Domain.MetaLearning;

namespace Ouroboros.Tests.MetaLearning;

/// <summary>
/// Unit tests for <see cref="SimpleModel"/>.
/// </summary>
[Trait("Category", "Unit")]
public class SimpleModelTests
{
    // --- Constructor ---

    [Fact]
    public void Constructor_NullPredictionFunc_ThrowsArgumentNullException()
    {
        var act = () => new SimpleModel(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_ValidFunc_DoesNotThrow()
    {
        var act = () => new SimpleModel((input, _) => input.ToUpperInvariant());

        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithInitialParameters_StoresParameters()
    {
        // Arrange
        var parameters = new Dictionary<string, object> { ["weight"] = 1.5 };

        // Act
        var model = new SimpleModel((_, _) => "ok", parameters);

        // Assert
        var result = model.GetParametersAsync().GetAwaiter().GetResult();
        result.Should().ContainKey("weight");
        result["weight"].Should().Be(1.5);
    }

    [Fact]
    public void Constructor_NullParameters_CreatesEmptyDictionary()
    {
        var model = new SimpleModel((_, _) => "ok");
        var result = model.GetParametersAsync().GetAwaiter().GetResult();

        result.Should().BeEmpty();
    }

    // --- PredictAsync ---

    [Fact]
    public async Task PredictAsync_CallsPredictionFunc()
    {
        // Arrange
        var model = new SimpleModel(
            (input, _) => $"Prediction for: {input}");

        // Act
        var result = await model.PredictAsync("test input");

        // Assert
        result.Should().Be("Prediction for: test input");
    }

    [Fact]
    public async Task PredictAsync_PassesParametersToFunc()
    {
        // Arrange
        var parameters = new Dictionary<string, object> { ["prefix"] = "Hello" };
        var model = new SimpleModel(
            (input, p) => $"{p["prefix"]} {input}",
            parameters);

        // Act
        var result = await model.PredictAsync("world");

        // Assert
        result.Should().Be("Hello world");
    }

    [Fact]
    public async Task PredictAsync_WhenCancelled_ThrowsOperationCancelledException()
    {
        // Arrange
        var model = new SimpleModel((_, _) => "result");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => model.PredictAsync("input", cts.Token));
    }

    // --- CloneAsync ---

    [Fact]
    public async Task CloneAsync_ReturnsNewInstance()
    {
        // Arrange
        var model = new SimpleModel(
            (input, _) => input,
            new Dictionary<string, object> { ["w"] = 1.0 });

        // Act
        var clone = await model.CloneAsync();

        // Assert
        clone.Should().NotBeSameAs(model);
    }

    [Fact]
    public async Task CloneAsync_CopiesParameters()
    {
        // Arrange
        var model = new SimpleModel(
            (input, _) => input,
            new Dictionary<string, object> { ["w"] = 2.5 });

        // Act
        var clone = await model.CloneAsync();
        var cloneParams = await clone.GetParametersAsync();

        // Assert
        cloneParams.Should().ContainKey("w");
        cloneParams["w"].Should().Be(2.5);
    }

    [Fact]
    public async Task CloneAsync_ParameterChangesDoNotAffectOriginal()
    {
        // Arrange
        var model = new SimpleModel(
            (input, _) => input,
            new Dictionary<string, object> { ["w"] = 1.0 });

        // Act
        var clone = await model.CloneAsync();
        await clone.UpdateParametersAsync(
            new Dictionary<string, object> { ["w"] = 10.0 }, 1.0);

        // Assert — original unchanged
        var originalParams = await model.GetParametersAsync();
        originalParams["w"].Should().Be(1.0);
    }

    [Fact]
    public async Task CloneAsync_WhenCancelled_ThrowsOperationCancelledException()
    {
        var model = new SimpleModel((_, _) => "x");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => model.CloneAsync(cts.Token));
    }

    // --- UpdateParametersAsync ---

    [Fact]
    public async Task UpdateParametersAsync_WithDoubleGradient_AppliesGradientDescent()
    {
        // Arrange — param = 5.0, gradient = 2.0, lr = 0.1
        // Expected: 5.0 - (0.1 * 2.0) = 4.8
        var model = new SimpleModel(
            (_, _) => "x",
            new Dictionary<string, object> { ["w"] = 5.0 });

        // Act
        await model.UpdateParametersAsync(
            new Dictionary<string, object> { ["w"] = 2.0 }, 0.1);

        // Assert
        var parameters = await model.GetParametersAsync();
        ((double)parameters["w"]).Should().BeApproximately(4.8, 0.001);
    }

    [Fact]
    public async Task UpdateParametersAsync_WithArrayGradient_AppliesElementwise()
    {
        // Arrange — param = [4.0, 6.0], gradient = [1.0, 2.0], lr = 0.5
        // Expected: [4.0 - 0.5*1.0, 6.0 - 0.5*2.0] = [3.5, 5.0]
        var model = new SimpleModel(
            (_, _) => "x",
            new Dictionary<string, object> { ["w"] = new double[] { 4.0, 6.0 } });

        // Act
        await model.UpdateParametersAsync(
            new Dictionary<string, object> { ["w"] = new double[] { 1.0, 2.0 } }, 0.5);

        // Assert
        var parameters = await model.GetParametersAsync();
        var result = (double[])parameters["w"];
        result[0].Should().BeApproximately(3.5, 0.001);
        result[1].Should().BeApproximately(5.0, 0.001);
    }

    [Fact]
    public async Task UpdateParametersAsync_WithNewKey_AddsGradientAsValue()
    {
        // Arrange — no existing key "bias"
        var model = new SimpleModel(
            (_, _) => "x",
            new Dictionary<string, object> { ["w"] = 1.0 });

        // Act
        await model.UpdateParametersAsync(
            new Dictionary<string, object> { ["bias"] = 0.5 }, 0.1);

        // Assert — new key gets the gradient value directly
        var parameters = await model.GetParametersAsync();
        parameters.Should().ContainKey("bias");
        parameters["bias"].Should().Be(0.5);
    }

    [Fact]
    public async Task UpdateParametersAsync_WhenCancelled_ThrowsOperationCancelledException()
    {
        var model = new SimpleModel((_, _) => "x");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => model.UpdateParametersAsync(new Dictionary<string, object>(), 0.1, cts.Token));
    }

    // --- GetParametersAsync ---

    [Fact]
    public async Task GetParametersAsync_ReturnsDefensiveCopy()
    {
        // Arrange
        var model = new SimpleModel(
            (_, _) => "x",
            new Dictionary<string, object> { ["w"] = 1.0 });

        // Act
        var params1 = await model.GetParametersAsync();
        params1["w"] = 999.0;

        var params2 = await model.GetParametersAsync();

        // Assert — original not modified
        params2["w"].Should().Be(1.0);
    }

    // --- SetParametersAsync ---

    [Fact]
    public async Task SetParametersAsync_ReplacesAllParameters()
    {
        // Arrange
        var model = new SimpleModel(
            (_, _) => "x",
            new Dictionary<string, object> { ["w"] = 1.0, ["b"] = 2.0 });

        // Act
        await model.SetParametersAsync(new Dictionary<string, object> { ["new_w"] = 3.0 });

        // Assert
        var parameters = await model.GetParametersAsync();
        parameters.Should().ContainKey("new_w");
        parameters.Should().NotContainKey("w");
        parameters.Should().NotContainKey("b");
    }

    [Fact]
    public async Task SetParametersAsync_WhenCancelled_ThrowsOperationCancelledException()
    {
        var model = new SimpleModel((_, _) => "x");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => model.SetParametersAsync(new Dictionary<string, object>(), cts.Token));
    }

    // --- ComputeGradientsAsync ---

    [Fact]
    public async Task ComputeGradientsAsync_WithDoubleParams_ReturnsGradients()
    {
        // Arrange
        var model = new SimpleModel(
            (_, _) => "x",
            new Dictionary<string, object> { ["w"] = 1.0 });

        var examples = new List<Example>
        {
            new("input1", "output1"),
            new("input2", "output2")
        };

        // Act
        var gradients = await model.ComputeGradientsAsync(examples);

        // Assert
        gradients.Should().ContainKey("w");
        gradients["w"].Should().BeOfType<double>();
    }

    [Fact]
    public async Task ComputeGradientsAsync_WithArrayParams_ReturnsArrayGradients()
    {
        // Arrange
        var model = new SimpleModel(
            (_, _) => "x",
            new Dictionary<string, object> { ["w"] = new double[] { 1.0, 2.0, 3.0 } });

        var examples = new List<Example> { new("a", "b") };

        // Act
        var gradients = await model.ComputeGradientsAsync(examples);

        // Assert
        gradients.Should().ContainKey("w");
        var gradArray = gradients["w"].Should().BeOfType<double[]>().Subject;
        gradArray.Should().HaveCount(3);
    }

    [Fact]
    public async Task ComputeGradientsAsync_WithNoParams_ReturnsEmptyGradients()
    {
        // Arrange
        var model = new SimpleModel((_, _) => "x");
        var examples = new List<Example> { new("a", "b") };

        // Act
        var gradients = await model.ComputeGradientsAsync(examples);

        // Assert
        gradients.Should().BeEmpty();
    }

    [Fact]
    public async Task ComputeGradientsAsync_WhenCancelled_ThrowsOperationCancelledException()
    {
        var model = new SimpleModel((_, _) => "x",
            new Dictionary<string, object> { ["w"] = 1.0 });
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => model.ComputeGradientsAsync(new List<Example>(), cts.Token));
    }
}
