// <copyright file="FaceDetectionExtensions.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ouroboros.Tensor.Abstractions;
using Ouroboros.Tensor.Adapters;
using Ouroboros.Tensor.Orchestration;

namespace Ouroboros.Tensor.Extensions;

/// <summary>
/// Phase 237 DI helpers for the <see cref="IFaceDetector"/> seam. Mirrors the
/// shape of <see cref="ExpressionClassificationExtensions"/>.
/// </summary>
public static class FaceDetectionExtensions
{
    /// <summary>
    /// Registers <see cref="StubFaceDetector"/> unconditionally (idempotent via
    /// <c>TryAddSingleton</c>). Fast-path for tests and headless containers.
    /// </summary>
    /// <param name="services">The DI container.</param>
    /// <returns><paramref name="services"/> for fluent chaining.</returns>
    public static IServiceCollection AddStubFaceDetector(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IFaceDetector, StubFaceDetector>();
        return services;
    }

    /// <summary>
    /// Registers an <see cref="IFaceDetector"/> with file-existence-based selection:
    /// <see cref="YuNetOnnxFaceDetector"/> when the YuNet model file is present,
    /// otherwise <see cref="StubFaceDetector"/>. The selection is cached as a
    /// singleton at first resolution.
    /// </summary>
    /// <param name="services">The DI container.</param>
    /// <param name="modelPath">
    ///   Resolved absolute path to <c>yunet_face_detection.onnx</c>. May be null
    ///   or missing — fallback to stub is silent except for one startup INFO log.
    /// </param>
    /// <returns><paramref name="services"/> for fluent chaining.</returns>
    public static IServiceCollection AddFaceDetectorWithYuNetFallback(
        this IServiceCollection services,
        string? modelPath)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IFaceDetector>(sp =>
        {
            var factoryLogger = sp.GetService<ILoggerFactory>()?
                .CreateLogger("Ouroboros.Tensor.Adapters.FaceDetectorFactory");

            if (!string.IsNullOrWhiteSpace(modelPath) && File.Exists(modelPath))
            {
                try
                {
                    var budget = sp.GetRequiredService<IPerceptionVramBudget>();
                    var scheduler = sp.GetRequiredService<GpuScheduler>();
                    var detectorLogger = sp.GetService<ILogger<YuNetOnnxFaceDetector>>();
                    var sessionFactory = sp.GetService<ISharedOrtDmlSessionFactory>();
                    factoryLogger?.LogInformation(
                        "IFaceDetector → YuNetOnnxFaceDetector (model={ModelPath})",
                        modelPath);
                    return new YuNetOnnxFaceDetector(modelPath, budget, scheduler, detectorLogger, sessionFactory);
                }
#pragma warning disable CA1031 // ONNX init failure must fall back to stub.
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    factoryLogger?.LogWarning(
                        ex,
                        "IFaceDetector → YuNetOnnxFaceDetector failed to initialize; falling back to StubFaceDetector");
                }
            }
            else
            {
                factoryLogger?.LogInformation(
                    "IFaceDetector → StubFaceDetector (YuNet model not found at '{ModelPath}')",
                    modelPath ?? "<unset>");
            }

            var stubLogger = sp.GetService<ILogger<StubFaceDetector>>();
            return new StubFaceDetector(stubLogger);
        });

        return services;
    }
}
