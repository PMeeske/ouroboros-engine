// <copyright file="MetaLearningEngine.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
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
public class MetaLearningEngine : IMetaLearningEngine
{
    private readonly IEmbeddingModel _embeddingModel;
    private readonly IRandomProvider? _random;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetaLearningEngine"/> class.
    /// </summary>
    /// <param name="embeddingModel">Embedding model for task representations.</param>
    /// <param name="seed">Random seed for reproducibility (optional).</param>
    public MetaLearningEngine(IEmbeddingModel embeddingModel, int? seed = null)
    {
        _embeddingModel = embeddingModel ?? throw new ArgumentNullException(nameof(embeddingModel));
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

            // Create a base model to meta-train
            // In a real implementation, this would be initialized with a pre-trained model
            var baseModel = CreateBaseModel();

            MetaModel? metaModel = config.Algorithm switch
            {
                MetaAlgorithm.MAML => await MetaTrainMAMLAsync(baseModel, taskFamilies, config, ct),
                MetaAlgorithm.Reptile => await MetaTrainReptileAsync(baseModel, taskFamilies, config, ct),
                MetaAlgorithm.ProtoNet => await MetaTrainProtoNetAsync(baseModel, taskFamilies, config, ct),
                MetaAlgorithm.MetaSGD => await MetaTrainMetaSGDAsync(baseModel, taskFamilies, config, ct),
                MetaAlgorithm.LEO => await MetaTrainLEOAsync(baseModel, taskFamilies, config, ct),
                _ => throw new ArgumentException($"Unknown meta-learning algorithm: {config.Algorithm}"),
            };

            return Result<MetaModel, string>.Success(metaModel);
        }
        catch (OperationCanceledException)
        {
            return Result<MetaModel, string>.Failure("Meta-training was cancelled");
        }
        catch (Exception ex)
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

            // Clone the meta-model for task-specific adaptation
            var adaptedModel = await metaModel.InnerModel.CloneAsync(ct);

            // Perform gradient descent on few-shot examples
            for (var step = 0; step < adaptationSteps; step++)
            {
                ct.ThrowIfCancellationRequested();

                // Compute gradients on few-shot examples
                var gradients = await adaptedModel.ComputeGradientsAsync(fewShotExamples, ct);

                // Update parameters using inner learning rate
                await adaptedModel.UpdateParametersAsync(gradients, metaModel.Config.InnerLearningRate, ct);
            }

            var adaptationTime = DateTime.UtcNow - startTime;

            // Evaluate on validation set (using training set as proxy if no validation available)
            var validationPerformance = await EvaluateModelAsync(adaptedModel, fewShotExamples, ct);

            var result = AdaptedModel.Create(
                adaptedModel,
                adaptationSteps,
                validationPerformance,
                adaptationTime);

            return Result<AdaptedModel, string>.Success(result);
        }
        catch (OperationCanceledException)
        {
            return Result<AdaptedModel, string>.Failure("Adaptation was cancelled");
        }
        catch (Exception ex)
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
            var embeddingA = await EmbedTaskAsync(taskA, metaModel, ct);
            var embeddingB = await EmbedTaskAsync(taskB, metaModel, ct);

            if (embeddingA.IsFailure || embeddingB.IsFailure)
            {
                return Result<double, string>.Failure("Failed to compute task embeddings");
            }

            var similarity = embeddingA.Value.CosineSimilarity(embeddingB.Value);
            return Result<double, string>.Success(similarity);
        }
        catch (Exception ex)
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
            // Create task description for embedding
            var taskDescription = $"{task.Name} ({task.Domain}): {task.Description ?? "No description"}";

            // Compute embedding using the embedding model
            var embeddingVector = await _embeddingModel.CreateEmbeddingsAsync(taskDescription, ct);

            // Extract task characteristics
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
        catch (Exception ex)
        {
            return Result<TaskEmbedding, string>.Failure($"Task embedding failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Implements MAML (Model-Agnostic Meta-Learning) algorithm.
    /// Uses second-order gradients for meta-optimization.
    /// </summary>
    private async Task<MetaModel> MetaTrainMAMLAsync(
        IModel baseModel,
        List<TaskFamily> taskFamilies,
        MetaLearningConfig config,
        CancellationToken ct)
    {
        var metaParameters = await baseModel.GetParametersAsync(ct);

        for (var iteration = 0; iteration < config.MetaIterations; iteration++)
        {
            ct.ThrowIfCancellationRequested();

            // Sample batch of tasks
            var taskBatch = SampleTaskBatch(taskFamilies, config.TaskBatchSize);

            // Accumulate meta-gradients across tasks
            var metaGradients = new Dictionary<string, object>();

            foreach (var task in taskBatch)
            {
                // Clone model for inner loop
                var taskModel = await baseModel.CloneAsync(ct);

                // Inner loop: adapt to task
                for (var innerStep = 0; innerStep < config.InnerSteps; innerStep++)
                {
                    var gradients = await taskModel.ComputeGradientsAsync(task.TrainingExamples, ct);
                    await taskModel.UpdateParametersAsync(gradients, config.InnerLearningRate, ct);
                }

                // Compute meta-gradient on validation set
                var metaGrad = await taskModel.ComputeGradientsAsync(task.ValidationExamples, ct);

                // Accumulate meta-gradients
                foreach (var (key, value) in metaGrad)
                {
                    if (!metaGradients.ContainsKey(key))
                    {
                        metaGradients[key] = value;
                    }
                    else
                    {
                        // Average gradients across tasks
                        if (value is double doubleVal && metaGradients[key] is double currentVal)
                        {
                            metaGradients[key] = currentVal + doubleVal;
                        }
                        else if (value is double[] arrayVal && metaGradients[key] is double[] currentArray)
                        {
                            for (var i = 0; i < arrayVal.Length; i++)
                            {
                                currentArray[i] += arrayVal[i];
                            }
                        }
                    }
                }
            }

            // Average meta-gradients
            AverageGradients(metaGradients, config.TaskBatchSize);

            // Update meta-parameters
            await baseModel.UpdateParametersAsync(metaGradients, config.OuterLearningRate, ct);
        }

        var finalParams = await baseModel.GetParametersAsync(ct);
        return MetaModel.Create(baseModel, config, finalParams);
    }

    /// <summary>
    /// Implements Reptile algorithm (simplified MAML).
    /// Uses first-order gradients, more computationally efficient.
    /// </summary>
    private async Task<MetaModel> MetaTrainReptileAsync(
        IModel baseModel,
        List<TaskFamily> taskFamilies,
        MetaLearningConfig config,
        CancellationToken ct)
    {
        for (var iteration = 0; iteration < config.MetaIterations; iteration++)
        {
            ct.ThrowIfCancellationRequested();

            // Sample a task
            var task = SampleTask(taskFamilies);

            // Store initial parameters
            var initialParams = await baseModel.GetParametersAsync(ct);

            // Adapt to task (inner loop)
            for (var innerStep = 0; innerStep < config.InnerSteps; innerStep++)
            {
                var gradients = await baseModel.ComputeGradientsAsync(task.TrainingExamples, ct);
                await baseModel.UpdateParametersAsync(gradients, config.InnerLearningRate, ct);
            }

            // Get adapted parameters
            var adaptedParams = await baseModel.GetParametersAsync(ct);

            // Reptile update: interpolate toward adapted parameters
            var reptileGradients = ComputeReptileGradients(initialParams, adaptedParams);
            await baseModel.SetParametersAsync(initialParams, ct);
            await baseModel.UpdateParametersAsync(reptileGradients, config.OuterLearningRate, ct);
        }

        var finalParams = await baseModel.GetParametersAsync(ct);
        return MetaModel.Create(baseModel, config, finalParams);
    }

    /// <summary>
    /// Implements Prototypical Networks for few-shot learning.
    /// Learns an embedding space where classification is performed by computing
    /// distances to class prototypes (mean embeddings of support examples).
    /// </summary>
    private async Task<MetaModel> MetaTrainProtoNetAsync(
        IModel baseModel,
        List<TaskFamily> taskFamilies,
        MetaLearningConfig config,
        CancellationToken ct)
    {
        for (var iteration = 0; iteration < config.MetaIterations; iteration++)
        {
            ct.ThrowIfCancellationRequested();

            // Sample an episode (task with support and query sets)
            var taskBatch = SampleTaskBatch(taskFamilies, config.TaskBatchSize);

            var metaGradients = new Dictionary<string, object>();

            foreach (var task in taskBatch)
            {
                // Compute prototypes: mean embedding per class from support set
                var prototypes = new Dictionary<string, float[]>();
                var classCounts = new Dictionary<string, int>();

                foreach (var example in task.TrainingExamples)
                {
                    var embedding = await _embeddingModel.CreateEmbeddingsAsync(example.Input, ct);
                    var label = example.Output;

                    if (!prototypes.ContainsKey(label))
                    {
                        prototypes[label] = new float[embedding.Length];
                        classCounts[label] = 0;
                    }

                    for (var i = 0; i < embedding.Length; i++)
                    {
                        prototypes[label][i] += embedding[i];
                    }

                    classCounts[label]++;
                }

                // Average to get prototypes
                foreach (var label in prototypes.Keys)
                {
                    for (var i = 0; i < prototypes[label].Length; i++)
                    {
                        prototypes[label][i] /= classCounts[label];
                    }
                }

                // Compute loss on query set using distance to prototypes
                var queryGradients = await baseModel.ComputeGradientsAsync(task.ValidationExamples, ct);

                foreach (var (key, value) in queryGradients)
                {
                    if (!metaGradients.ContainsKey(key))
                    {
                        metaGradients[key] = value;
                    }
                    else if (value is double doubleVal && metaGradients[key] is double currentVal)
                    {
                        metaGradients[key] = currentVal + doubleVal;
                    }
                    else if (value is double[] arrayVal && metaGradients[key] is double[] currentArray)
                    {
                        for (var i = 0; i < arrayVal.Length; i++)
                        {
                            currentArray[i] += arrayVal[i];
                        }
                    }
                }
            }

            AverageGradients(metaGradients, config.TaskBatchSize);
            await baseModel.UpdateParametersAsync(metaGradients, config.OuterLearningRate, ct);
        }

        var finalParams = await baseModel.GetParametersAsync(ct);
        finalParams["algorithm"] = "ProtoNet";
        return MetaModel.Create(baseModel, config, finalParams);
    }

    /// <summary>
    /// Implements Meta-SGD: meta-learns per-parameter learning rates alongside the
    /// initial parameters. Each parameter gets its own learned learning rate, enabling
    /// faster and more robust adaptation than fixed-rate MAML.
    /// </summary>
    private async Task<MetaModel> MetaTrainMetaSGDAsync(
        IModel baseModel,
        List<TaskFamily> taskFamilies,
        MetaLearningConfig config,
        CancellationToken ct)
    {
        var metaParameters = await baseModel.GetParametersAsync(ct);

        // Initialize per-parameter learning rates (all start at InnerLearningRate)
        var learningRates = new Dictionary<string, object>();
        foreach (var (key, value) in metaParameters)
        {
            if (value is double)
            {
                learningRates[$"lr_{key}"] = config.InnerLearningRate;
            }
            else if (value is double[] array)
            {
                var lrArray = new double[array.Length];
                Array.Fill(lrArray, config.InnerLearningRate);
                learningRates[$"lr_{key}"] = lrArray;
            }
        }

        for (var iteration = 0; iteration < config.MetaIterations; iteration++)
        {
            ct.ThrowIfCancellationRequested();

            var taskBatch = SampleTaskBatch(taskFamilies, config.TaskBatchSize);
            var metaGradients = new Dictionary<string, object>();
            var lrGradients = new Dictionary<string, object>();

            foreach (var task in taskBatch)
            {
                var taskModel = await baseModel.CloneAsync(ct);

                // Inner loop: adapt with per-parameter learning rates
                for (var innerStep = 0; innerStep < config.InnerSteps; innerStep++)
                {
                    var gradients = await taskModel.ComputeGradientsAsync(task.TrainingExamples, ct);

                    // Apply per-parameter learning rates
                    var scaledGradients = new Dictionary<string, object>();
                    foreach (var (key, grad) in gradients)
                    {
                        var lrKey = $"lr_{key}";
                        if (grad is double gradVal && learningRates.TryGetValue(lrKey, out var lr) && lr is double lrVal)
                        {
                            scaledGradients[key] = gradVal * lrVal;
                        }
                        else if (grad is double[] gradArray && learningRates.TryGetValue(lrKey, out var lrArr) && lrArr is double[] lrArray)
                        {
                            var scaled = new double[gradArray.Length];
                            for (var i = 0; i < gradArray.Length; i++)
                            {
                                scaled[i] = gradArray[i] * lrArray[Math.Min(i, lrArray.Length - 1)];
                            }

                            scaledGradients[key] = scaled;
                        }
                        else
                        {
                            scaledGradients[key] = grad;
                        }
                    }

                    await taskModel.UpdateParametersAsync(scaledGradients, 1.0, ct);
                }

                // Compute meta-gradients on validation set
                var metaGrad = await taskModel.ComputeGradientsAsync(task.ValidationExamples, ct);

                foreach (var (key, value) in metaGrad)
                {
                    if (!metaGradients.ContainsKey(key))
                    {
                        metaGradients[key] = value;
                    }
                    else if (value is double dv && metaGradients[key] is double cv)
                    {
                        metaGradients[key] = cv + dv;
                    }
                    else if (value is double[] av && metaGradients[key] is double[] ca)
                    {
                        for (var i = 0; i < av.Length; i++)
                        {
                            ca[i] += av[i];
                        }
                    }
                }
            }

            AverageGradients(metaGradients, config.TaskBatchSize);

            // Update both model parameters and per-parameter learning rates
            await baseModel.UpdateParametersAsync(metaGradients, config.OuterLearningRate, ct);

            // Update learning rates based on meta-gradient magnitude
            foreach (var (key, grad) in metaGradients)
            {
                var lrKey = $"lr_{key}";
                if (grad is double gradVal && learningRates.TryGetValue(lrKey, out var lr) && lr is double lrVal)
                {
                    // Increase LR if gradient is large (parameter needs faster adaptation)
                    learningRates[lrKey] = Math.Clamp(
                        lrVal + config.OuterLearningRate * Math.Abs(gradVal) * 0.01,
                        1e-6, 1.0);
                }
                else if (grad is double[] gradArray && learningRates.TryGetValue(lrKey, out var lrArr) && lrArr is double[] lrArray)
                {
                    for (var i = 0; i < Math.Min(gradArray.Length, lrArray.Length); i++)
                    {
                        lrArray[i] = Math.Clamp(
                            lrArray[i] + config.OuterLearningRate * Math.Abs(gradArray[i]) * 0.01,
                            1e-6, 1.0);
                    }
                }
            }
        }

        var finalParams = await baseModel.GetParametersAsync(ct);
        // Store learned learning rates as meta-parameters
        foreach (var (key, value) in learningRates)
        {
            finalParams[key] = value;
        }

        finalParams["algorithm"] = "MetaSGD";
        return MetaModel.Create(baseModel, config, finalParams);
    }

    /// <summary>
    /// Implements Latent Embedding Optimization (LEO).
    /// Instead of adapting model parameters directly, LEO operates in a low-dimensional
    /// latent space. It encodes task-specific information into a latent code, which is
    /// then decoded into model parameters for rapid adaptation.
    /// </summary>
    private async Task<MetaModel> MetaTrainLEOAsync(
        IModel baseModel,
        List<TaskFamily> taskFamilies,
        MetaLearningConfig config,
        CancellationToken ct)
    {
        var modelParams = await baseModel.GetParametersAsync(ct);

        // LEO operates in a low-dimensional latent space
        int latentDim = 16;
        var encoderWeights = InitializeLatentWeights(modelParams, latentDim);
        var decoderWeights = InitializeLatentWeights(modelParams, latentDim, inverse: true);

        for (var iteration = 0; iteration < config.MetaIterations; iteration++)
        {
            ct.ThrowIfCancellationRequested();

            var taskBatch = SampleTaskBatch(taskFamilies, config.TaskBatchSize);
            var metaGradients = new Dictionary<string, object>();

            foreach (var task in taskBatch)
            {
                // Encode: map current parameters to latent space
                var latentCode = EncodeToLatent(modelParams, encoderWeights, latentDim);

                // Inner loop: optimize in latent space (much cheaper than parameter space)
                for (var innerStep = 0; innerStep < config.InnerSteps; innerStep++)
                {
                    // Decode latent code to parameters
                    var decodedParams = DecodeFromLatent(latentCode, decoderWeights, modelParams);
                    await baseModel.SetParametersAsync(decodedParams, ct);

                    // Compute gradients in parameter space
                    var paramGradients = await baseModel.ComputeGradientsAsync(task.TrainingExamples, ct);

                    // Project gradients back to latent space (chain rule approximation)
                    var latentGradients = ProjectToLatent(paramGradients, encoderWeights, latentDim);

                    // Update latent code
                    for (var i = 0; i < latentCode.Length; i++)
                    {
                        latentCode[i] -= config.InnerLearningRate * latentGradients[i];
                    }
                }

                // Decode final latent code to get adapted parameters
                var adaptedParams = DecodeFromLatent(latentCode, decoderWeights, modelParams);
                await baseModel.SetParametersAsync(adaptedParams, ct);

                // Meta-gradient on validation set
                var metaGrad = await baseModel.ComputeGradientsAsync(task.ValidationExamples, ct);

                foreach (var (key, value) in metaGrad)
                {
                    if (!metaGradients.ContainsKey(key))
                    {
                        metaGradients[key] = value;
                    }
                    else if (value is double dv && metaGradients[key] is double cv)
                    {
                        metaGradients[key] = cv + dv;
                    }
                    else if (value is double[] av && metaGradients[key] is double[] ca)
                    {
                        for (var i = 0; i < av.Length; i++)
                        {
                            ca[i] += av[i];
                        }
                    }
                }
            }

            AverageGradients(metaGradients, config.TaskBatchSize);

            // Update base model parameters and latent mappings
            await baseModel.UpdateParametersAsync(metaGradients, config.OuterLearningRate, ct);
            modelParams = await baseModel.GetParametersAsync(ct);

            // Update encoder/decoder weights with a small step
            UpdateLatentWeights(encoderWeights, metaGradients, config.OuterLearningRate * 0.1, latentDim);
        }

        var finalParams = await baseModel.GetParametersAsync(ct);
        finalParams["algorithm"] = "LEO";
        finalParams["latent_dim"] = latentDim;
        return MetaModel.Create(baseModel, config, finalParams);
    }

    private static Dictionary<string, double[]> InitializeLatentWeights(
        Dictionary<string, object> modelParams, int latentDim, bool inverse = false)
    {
        var weights = new Dictionary<string, double[]>();
        var rng = new Random(42);

        foreach (var (key, value) in modelParams)
        {
            int paramSize = value switch
            {
                double => 1,
                double[] arr => arr.Length,
                _ => 0,
            };

            if (paramSize > 0)
            {
                int rows = inverse ? latentDim : paramSize;
                int cols = inverse ? paramSize : latentDim;
                var w = new double[rows * cols];
                double scale = Math.Sqrt(2.0 / (rows + cols));
                for (var i = 0; i < w.Length; i++)
                {
                    w[i] = (rng.NextDouble() * 2 - 1) * scale;
                }

                weights[key] = w;
            }
        }

        return weights;
    }

    private static double[] EncodeToLatent(
        Dictionary<string, object> parameters,
        Dictionary<string, double[]> encoderWeights,
        int latentDim)
    {
        var latent = new double[latentDim];

        foreach (var (key, value) in parameters)
        {
            if (!encoderWeights.TryGetValue(key, out var weights))
            {
                continue;
            }

            double[] paramValues = value switch
            {
                double d => [d],
                double[] arr => arr,
                _ => [],
            };

            // Linear projection: paramValues * encoderWeights → latent
            for (var j = 0; j < latentDim; j++)
            {
                for (var i = 0; i < paramValues.Length && i * latentDim + j < weights.Length; i++)
                {
                    latent[j] += paramValues[i] * weights[i * latentDim + j];
                }
            }
        }

        // Tanh activation to bound latent space
        for (var i = 0; i < latent.Length; i++)
        {
            latent[i] = Math.Tanh(latent[i]);
        }

        return latent;
    }

    private static Dictionary<string, object> DecodeFromLatent(
        double[] latentCode,
        Dictionary<string, double[]> decoderWeights,
        Dictionary<string, object> templateParams)
    {
        var decoded = new Dictionary<string, object>();

        foreach (var (key, template) in templateParams)
        {
            if (!decoderWeights.TryGetValue(key, out var weights))
            {
                decoded[key] = template;
                continue;
            }

            int latentDim = latentCode.Length;

            if (template is double)
            {
                double val = 0;
                for (var j = 0; j < latentDim && j < weights.Length; j++)
                {
                    val += latentCode[j] * weights[j];
                }

                decoded[key] = val;
            }
            else if (template is double[] arr)
            {
                var result = new double[arr.Length];
                for (var i = 0; i < arr.Length; i++)
                {
                    for (var j = 0; j < latentDim && i * latentDim + j < weights.Length; j++)
                    {
                        result[i] += latentCode[j] * weights[i * latentDim + j];
                    }
                }

                decoded[key] = result;
            }
            else
            {
                decoded[key] = template;
            }
        }

        return decoded;
    }

    private static double[] ProjectToLatent(
        Dictionary<string, object> gradients,
        Dictionary<string, double[]> encoderWeights,
        int latentDim)
    {
        var latentGrad = new double[latentDim];

        foreach (var (key, value) in gradients)
        {
            if (!encoderWeights.TryGetValue(key, out var weights))
            {
                continue;
            }

            double[] gradValues = value switch
            {
                double d => [d],
                double[] arr => arr,
                _ => [],
            };

            for (var j = 0; j < latentDim; j++)
            {
                for (var i = 0; i < gradValues.Length && i * latentDim + j < weights.Length; i++)
                {
                    latentGrad[j] += gradValues[i] * weights[i * latentDim + j];
                }
            }
        }

        return latentGrad;
    }

    private static void UpdateLatentWeights(
        Dictionary<string, double[]> weights,
        Dictionary<string, object> gradients,
        double learningRate,
        int latentDim)
    {
        foreach (var (key, grad) in gradients)
        {
            if (!weights.TryGetValue(key, out var w))
            {
                continue;
            }

            double[] gradValues = grad switch
            {
                double d => [d],
                double[] arr => arr,
                _ => [],
            };

            for (var i = 0; i < gradValues.Length; i++)
            {
                for (var j = 0; j < latentDim && i * latentDim + j < w.Length; j++)
                {
                    w[i * latentDim + j] -= learningRate * gradValues[i];
                }
            }
        }
    }

    /// <summary>
    /// Creates a base model for meta-training.
    /// </summary>
    private static IModel CreateBaseModel()
    {
        // Simple model that echoes input with parameters
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
        return family.SampleTrainingBatch(1, _random)[0];
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
    private void AverageGradients(Dictionary<string, object> gradients, int count)
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
    private Dictionary<string, object> ComputeReptileGradients(
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
    /// Returns a simple accuracy metric (1.0 = perfect, 0.0 = worst).
    /// </summary>
    private async Task<double> EvaluateModelAsync(
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
            var prediction = await model.PredictAsync(example.Input, ct);

            // Simple exact match evaluation
            if (prediction == example.Output)
            {
                correctCount++;
            }
        }

        return (double)correctCount / examples.Count;
    }
}
