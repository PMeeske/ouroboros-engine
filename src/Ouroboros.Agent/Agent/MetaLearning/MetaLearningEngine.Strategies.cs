// <copyright file="MetaLearningEngine.Strategies.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Domain.MetaLearning;

namespace Ouroboros.Agent.MetaLearning;

/// <summary>
/// Meta-learning algorithm implementations: MAML, Reptile, ProtoNet, Meta-SGD, LEO.
/// </summary>
public partial class MetaLearningEngine
{
    /// <summary>
    /// Implements Model-Agnostic Meta-Learning (MAML).
    /// Finds initial parameters that can be quickly adapted to new tasks using a small
    /// number of gradient steps. Uses second-order gradients via a bi-level optimization.
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

            var taskBatch = SampleTaskBatch(taskFamilies, config.TaskBatchSize);
            var metaGradients = new Dictionary<string, object>();

            foreach (var task in taskBatch)
            {
                var taskModel = await baseModel.CloneAsync(ct);

                for (var innerStep = 0; innerStep < config.InnerSteps; innerStep++)
                {
                    var gradients = await taskModel.ComputeGradientsAsync(task.TrainingExamples, ct);
                    await taskModel.UpdateParametersAsync(gradients, config.InnerLearningRate, ct);
                }

                var metaGrad = await taskModel.ComputeGradientsAsync(task.ValidationExamples, ct);

                foreach (var (key, value) in metaGrad)
                {
                    if (!metaGradients.ContainsKey(key))
                    {
                        metaGradients[key] = value;
                    }
                    else
                    {
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

            AverageGradients(metaGradients, config.TaskBatchSize);
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

            var task = SampleTask(taskFamilies);
            var initialParams = await baseModel.GetParametersAsync(ct);

            for (var innerStep = 0; innerStep < config.InnerSteps; innerStep++)
            {
                var gradients = await baseModel.ComputeGradientsAsync(task.TrainingExamples, ct);
                await baseModel.UpdateParametersAsync(gradients, config.InnerLearningRate, ct);
            }

            var adaptedParams = await baseModel.GetParametersAsync(ct);
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

            var taskBatch = SampleTaskBatch(taskFamilies, config.TaskBatchSize);
            var metaGradients = new Dictionary<string, object>();

            foreach (var task in taskBatch)
            {
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

                foreach (var label in prototypes.Keys)
                {
                    for (var i = 0; i < prototypes[label].Length; i++)
                    {
                        prototypes[label][i] /= classCounts[label];
                    }
                }

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

            foreach (var task in taskBatch)
            {
                var taskModel = await baseModel.CloneAsync(ct);

                for (var innerStep = 0; innerStep < config.InnerSteps; innerStep++)
                {
                    var gradients = await taskModel.ComputeGradientsAsync(task.TrainingExamples, ct);

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
            await baseModel.UpdateParametersAsync(metaGradients, config.OuterLearningRate, ct);

            foreach (var (key, grad) in metaGradients)
            {
                var lrKey = $"lr_{key}";
                if (grad is double gradVal && learningRates.TryGetValue(lrKey, out var lr) && lr is double lrVal)
                {
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
                var latentCode = EncodeToLatent(modelParams, encoderWeights, latentDim);

                for (var innerStep = 0; innerStep < config.InnerSteps; innerStep++)
                {
                    var decodedParams = DecodeFromLatent(latentCode, decoderWeights, modelParams);
                    await baseModel.SetParametersAsync(decodedParams, ct);

                    var paramGradients = await baseModel.ComputeGradientsAsync(task.TrainingExamples, ct);
                    var latentGradients = ProjectToLatent(paramGradients, encoderWeights, latentDim);

                    for (var i = 0; i < latentCode.Length; i++)
                    {
                        latentCode[i] -= config.InnerLearningRate * latentGradients[i];
                    }
                }

                var adaptedParams = DecodeFromLatent(latentCode, decoderWeights, modelParams);
                await baseModel.SetParametersAsync(adaptedParams, ct);

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
            await baseModel.UpdateParametersAsync(metaGradients, config.OuterLearningRate, ct);
            modelParams = await baseModel.GetParametersAsync(ct);
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

            for (var j = 0; j < latentDim; j++)
            {
                for (var i = 0; i < paramValues.Length && i * latentDim + j < weights.Length; i++)
                {
                    latent[j] += paramValues[i] * weights[i * latentDim + j];
                }
            }
        }

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
}
