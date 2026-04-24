// <copyright file="FaceEmbeddingExtensions.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ouroboros.Tensor.Adapters;
using Ouroboros.Tensor.Orchestration;

namespace Ouroboros.Tensor.Extensions;

/// <summary>Phase 239 DI helpers for <see cref="IFaceEmbedder"/>.</summary>
public static class FaceEmbeddingExtensions
{
    /// <summary>Registers <see cref="StubFaceEmbedder"/> unconditionally (idempotent).</summary>
    /// <param name="services">The DI container.</param>
    /// <returns><paramref name="services"/> for fluent chaining.</returns>
    public static IServiceCollection AddStubFaceEmbedder(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IFaceEmbedder, StubFaceEmbedder>();
        return services;
    }

    /// <summary>
    /// Registers <see cref="IFaceEmbedder"/> with file-existence selection:
    /// <see cref="SFaceOnnxEmbedder"/> when the SFace model file is present,
    /// otherwise <see cref="StubFaceEmbedder"/>.
    /// </summary>
    /// <param name="services">The DI container.</param>
    /// <param name="modelPath">Resolved absolute path to <c>sface_recognition.onnx</c>.</param>
    /// <returns><paramref name="services"/> for fluent chaining.</returns>
    public static IServiceCollection AddFaceEmbedderWithSFaceFallback(
        this IServiceCollection services,
        string? modelPath)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IFaceEmbedder>(sp =>
        {
            var factoryLogger = sp.GetService<ILoggerFactory>()?
                .CreateLogger("Ouroboros.Tensor.Adapters.FaceEmbedderFactory");

            if (!string.IsNullOrWhiteSpace(modelPath) && File.Exists(modelPath))
            {
                try
                {
                    var budget = sp.GetRequiredService<IPerceptionVramBudget>();
                    var scheduler = sp.GetRequiredService<GpuScheduler>();
                    var logger = sp.GetService<ILogger<SFaceOnnxEmbedder>>();
                    factoryLogger?.LogInformation(
                        "IFaceEmbedder → SFaceOnnxEmbedder (model={ModelPath})",
                        modelPath);
                    return new SFaceOnnxEmbedder(modelPath, budget, scheduler, logger);
                }
#pragma warning disable CA1031
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    factoryLogger?.LogWarning(
                        ex,
                        "IFaceEmbedder → SFaceOnnxEmbedder failed to initialize; falling back to StubFaceEmbedder");
                }
            }
            else
            {
                factoryLogger?.LogInformation(
                    "IFaceEmbedder → StubFaceEmbedder (SFace model not found at '{ModelPath}')",
                    modelPath ?? "<unset>");
            }

            var stubLogger = sp.GetService<ILogger<StubFaceEmbedder>>();
            return new StubFaceEmbedder(stubLogger);
        });

        return services;
    }
}
