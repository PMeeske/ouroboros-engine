// <copyright file="PoseGazeExtensions.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ouroboros.Tensor.Adapters;
using Ouroboros.Tensor.Orchestration;

namespace Ouroboros.Tensor.Extensions;

/// <summary>Phase 240 DI helpers for pose + gaze estimators.</summary>
public static class PoseGazeExtensions
{
    /// <summary>Registers <see cref="StubPoseEstimator"/> unconditionally.</summary>
    /// <param name="services">The DI container.</param>
    /// <returns><paramref name="services"/> for fluent chaining.</returns>
    public static IServiceCollection AddStubPoseEstimator(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IPoseEstimator, StubPoseEstimator>();
        return services;
    }

    /// <summary>Registers MoveNet ONNX with stub fallback.</summary>
    /// <param name="services">The DI container.</param>
    /// <param name="modelPath">Resolved absolute path to <c>movenet_thunder.onnx</c>.</param>
    /// <returns><paramref name="services"/> for fluent chaining.</returns>
    public static IServiceCollection AddPoseEstimatorWithMoveNetFallback(
        this IServiceCollection services,
        string? modelPath)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IPoseEstimator>(sp =>
        {
            var factoryLogger = sp.GetService<ILoggerFactory>()?
                .CreateLogger("Ouroboros.Tensor.Adapters.PoseEstimatorFactory");

            if (!string.IsNullOrWhiteSpace(modelPath) && File.Exists(modelPath))
            {
                try
                {
                    var budget = sp.GetRequiredService<IPerceptionVramBudget>();
                    var scheduler = sp.GetRequiredService<GpuScheduler>();
                    var logger = sp.GetService<ILogger<MoveNetOnnxPoseEstimator>>();
                    factoryLogger?.LogInformation(
                        "IPoseEstimator → MoveNetOnnxPoseEstimator (model={ModelPath})",
                        modelPath);
                    return new MoveNetOnnxPoseEstimator(modelPath, budget, scheduler, logger);
                }
#pragma warning disable CA1031
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    factoryLogger?.LogWarning(ex,
                        "IPoseEstimator → MoveNetOnnxPoseEstimator init failed; falling back");
                }
            }
            else
            {
                factoryLogger?.LogInformation(
                    "IPoseEstimator → StubPoseEstimator (MoveNet model not found at '{ModelPath}')",
                    modelPath ?? "<unset>");
            }

            return new StubPoseEstimator(sp.GetService<ILogger<StubPoseEstimator>>());
        });

        return services;
    }

    /// <summary>Registers <see cref="StubGazeEstimator"/> unconditionally.</summary>
    /// <param name="services">The DI container.</param>
    /// <returns><paramref name="services"/> for fluent chaining.</returns>
    public static IServiceCollection AddStubGazeEstimator(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IGazeEstimator, StubGazeEstimator>();
        return services;
    }

    /// <summary>Registers MobileGaze ONNX with stub fallback.</summary>
    /// <param name="services">The DI container.</param>
    /// <param name="modelPath">Resolved absolute path to <c>mobilegaze.onnx</c>.</param>
    /// <returns><paramref name="services"/> for fluent chaining.</returns>
    public static IServiceCollection AddGazeEstimatorWithMobileGazeFallback(
        this IServiceCollection services,
        string? modelPath)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IGazeEstimator>(sp =>
        {
            var factoryLogger = sp.GetService<ILoggerFactory>()?
                .CreateLogger("Ouroboros.Tensor.Adapters.GazeEstimatorFactory");

            if (!string.IsNullOrWhiteSpace(modelPath) && File.Exists(modelPath))
            {
                try
                {
                    var budget = sp.GetRequiredService<IPerceptionVramBudget>();
                    var scheduler = sp.GetRequiredService<GpuScheduler>();
                    var logger = sp.GetService<ILogger<MobileGazeOnnxEstimator>>();
                    factoryLogger?.LogInformation(
                        "IGazeEstimator → MobileGazeOnnxEstimator (model={ModelPath})",
                        modelPath);
                    return new MobileGazeOnnxEstimator(modelPath, budget, scheduler, logger);
                }
#pragma warning disable CA1031
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    factoryLogger?.LogWarning(ex,
                        "IGazeEstimator → MobileGazeOnnxEstimator init failed; falling back");
                }
            }
            else
            {
                factoryLogger?.LogInformation(
                    "IGazeEstimator → StubGazeEstimator (MobileGaze model not found at '{ModelPath}')",
                    modelPath ?? "<unset>");
            }

            return new StubGazeEstimator(sp.GetService<ILogger<StubGazeEstimator>>());
        });

        return services;
    }
}
