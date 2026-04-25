// <copyright file="MetaLearningEngine.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Core.Randomness;
using Ouroboros.Domain;
using Ouroboros.Domain.MetaLearning;
using Ouroboros.Providers.Random;

namespace Ouroboros.Agent.MetaLearning;

/// <summary>
/// Implements meta-learning algorithms for fast task adaptation.
/// Supports MAML, Reptile, ProtoNet, Meta-SGD, LEO, and task embedding computation.
/// </summary>
public partial class MetaLearningEngine : IMetaLearningEngine
{
    private readonly IEmbeddingModel _embeddingModel;
    private readonly IRandomProvider _random;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetaLearningEngine"/> class.
    /// </summary>
    /// <param name="embeddingModel">Embedding model for task representations.</param>
    /// <param name="seed">Random seed for reproducibility (optional).</param>
    public MetaLearningEngine(IEmbeddingModel embeddingModel, int? seed = null)
    {
        ArgumentNullException.ThrowIfNull(embeddingModel);
        _embeddingModel = embeddingModel;
        _random = seed.HasValue ? new SeededRandomProvider(seed.Value) : new SeededRandomProvider();
    }

    /// <inheritdoc/>
    public async Task<Result<MetaModel, string>> MetaTrainAsync(
        List<TaskFamily> taskFamilies,
        MetaLearningConfig config,
        CancellationToken ct = default)
    {
        try
        {
            if (taskFamilies == null || taskFamilies.Count == 0)
            {
                return Result<MetaModel, string>.Failure("Task families list cannot be empty");
            }

            var baseModel = CreateBaseModel();

            MetaModel? metaModel = config.Algorithm switch
            {
                MetaAlgorithm.MAML => await MetaTrainMAMLAsync(baseModel, taskFamilies, config, ct).ConfigureAwait(false),
                MetaAlgorithm.Reptile => await MetaTrainReptileAsync(baseModel, taskFamilies, config, ct).ConfigureAwait(false),
                MetaAlgorithm.ProtoNet => await MetaTrainProtoNetAsync(baseModel, taskFamilies, config, ct).ConfigureAwait(false),
                MetaAlgorithm.MetaSGD => await MetaTrainMetaSGDAsync(baseModel, taskFamilies, config, ct).ConfigureAwait(false),
                MetaAlgorithm.LEO => await MetaTrainLEOAsync(baseModel, taskFamilies, config, ct).ConfigureAwait(false),
                _ => throw new ArgumentException($"Unknown meta-learning algorithm: {config.Algorithm}"),
            };

            return Result<MetaModel, string>.Success(metaModel);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<MetaModel, string>.Failure($"Meta-training failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<AdaptedModel, string>> AdaptToTaskAsync(
        MetaModel metaModel,
        List<Example> fewShotExamples,
        int adaptationSteps,
        CancellationToken ct = default)
    {
        try
        {
            if (fewShotExamples == null || fewShotExamples.Count == 0)
            {
                return Result<AdaptedModel, string>.Failure("Few-shot examples cannot be empty");
            }

            if (adaptationSteps <= 0)
            {
                return Result<AdaptedModel, string>.Failure("Adaptation steps must be positive");
            }

            var startTime = DateTime.UtcNow;
            var adaptedModel = await metaModel.InnerModel.CloneAsync(ct).ConfigureAwait(false);

            for (var step = 0; step < adaptationSteps; step++)
            {
                ct.ThrowIfCancellationRequested();
                var gradients = await adaptedModel.ComputeGradientsAsync(fewShotExamples, ct).ConfigureAwait(false);
                await adaptedModel.UpdateParametersAsync(gradients, metaModel.Config.InnerLearningRate, ct).ConfigureAwait(false);
            }

            var adaptationTime = DateTime.UtcNow - startTime;
            var validationPerformance = await EvaluateModelAsync(adaptedModel, fewShotExamples, ct).ConfigureAwait(false);

            var result = AdaptedModel.Create(
                adaptedModel,
                adaptationSteps,
                validationPerformance,
                adaptationTime);

            return Result<AdaptedModel, string>.Success(result);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<AdaptedModel, string>.Failure($"Adaptation failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<double, string>> ComputeTaskSimilarityAsync(
        SynthesisTask taskA,
        SynthesisTask taskB,
        MetaModel metaModel,
        CancellationToken ct = default)
    {
        try
        {
            var embeddingA = await EmbedTaskAsync(taskA, metaModel, ct).ConfigureAwait(false);
            var embeddingB = await EmbedTaskAsync(taskB, metaModel, ct).ConfigureAwait(false);

            if (embeddingA.IsFailure || embeddingB.IsFailure)
            {
                return Result<double, string>.Failure("Failed to compute task embeddings");
            }

            var similarity = embeddingA.Value.CosineSimilarity(embeddingB.Value);
            return Result<double, string>.Success(similarity);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<double, string>.Failure($"Task similarity computation failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<TaskEmbedding, string>> EmbedTaskAsync(
        SynthesisTask task,
        MetaModel metaModel,
        CancellationToken ct = default)
    {
        try
        {
            var taskDescription = $"{task.Name} ({task.Domain}): {task.Description ?? "No description"}";
            var embeddingVector = await _embeddingModel.CreateEmbeddingsAsync(taskDescription, ct).ConfigureAwait(false);

            var characteristics = new Dictionary<string, double>
            {
                ["training_examples"] = task.TrainingExamples.Count,
                ["validation_examples"] = task.ValidationExamples.Count,
                ["total_examples"] = task.TotalExamples,
                ["avg_input_length"] = task.TrainingExamples.Average(e => e.Input.Length),
                ["avg_output_length"] = task.TrainingExamples.Average(e => e.Output.Length),
            };

            var embedding = new TaskEmbedding(embeddingVector, characteristics, taskDescription);
            return Result<TaskEmbedding, string>.Success(embedding);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<TaskEmbedding, string>.Failure($"Task embedding failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a base model for meta-training.
    /// </summary>
    private static IModel CreateBaseModel()
    {
        var initialParams = new Dictionary<string, object>
        {
            ["bias"] = 0.0,
            ["weights"] = new double[] { 1.0, 0.0, 0.0 },
        };

        return new SimpleModel(
            (input, parameters) => $"Response to: {input}",
            initialParams);
    }

    /// <summary>
    /// Samples a single task from task families.
    /// </summary>
    private SynthesisTask SampleTask(List<TaskFamily> taskFamilies)
    {
        var family = taskFamilies[_random.Next(taskFamilies.Count)];
        return family!.SampleTrainingBatch(1, _random).First();
    }

    /// <summary>
    /// Samples a batch of tasks from task families.
    /// </summary>
    private List<SynthesisTask> SampleTaskBatch(List<TaskFamily> taskFamilies, int batchSize)
    {
        var batch = new List<SynthesisTask>();
        for (var i = 0; i < batchSize; i++)
        {
            batch.Add(SampleTask(taskFamilies));
        }

        return batch;
    }

    /// <summary>
    /// Averages gradients in-place.
    /// </summary>
    private static void AverageGradients(Dictionary<string, object> gradients, int count)
    {
        foreach (var key in gradients.Keys.ToList())
        {
            if (gradients[key] is double doubleVal)
            {
                gradients[key] = doubleVal / count;
            }
            else if (gradients[key] is double[] arrayVal)
            {
                for (var i = 0; i < arrayVal.Length; i++)
                {
                    arrayVal[i] /= count;
                }
            }
        }
    }

    /// <summary>
    /// Computes Reptile gradients (difference between adapted and initial parameters).
    /// </summary>
    private static Dictionary<string, object> ComputeReptileGradients(
        Dictionary<string, object> initialParams,
        Dictionary<string, object> adaptedParams)
    {
        var gradients = new Dictionary<string, object>();

        foreach (var (key, initialValue) in initialParams)
        {
            if (!adaptedParams.ContainsKey(key))
            {
                continue;
            }

            var adaptedValue = adaptedParams[key];

            if (initialValue is double initDouble && adaptedValue is double adaptDouble)
            {
                gradients[key] = initDouble - adaptDouble;
            }
            else if (initialValue is double[] initArray && adaptedValue is double[] adaptArray)
            {
                var gradArray = new double[initArray.Length];
                for (var i = 0; i < initArray.Length; i++)
                {
                    gradArray[i] = initArray[i] - adaptArray[i];
                }

                gradients[key] = gradArray;
            }
        }

        return gradients;
    }

    /// <summary>
    /// Evaluates model performance on examples.
    /// </summary>
    private static async Task<double> EvaluateModelAsync(
        IModel model,
        List<Example> examples,
        CancellationToken ct)
    {
        if (examples.Count == 0)
        {
            return 0.0;
        }

        var correctCount = 0;
        foreach (var example in examples)
        {
            var prediction = await model.PredictAsync(example.Input, ct).ConfigureAwait(false);

            if (prediction == example.Output)
            {
                correctCount++;
            }
        }

        return (double)correctCount / examples.Count;
    }
}
