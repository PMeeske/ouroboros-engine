// <copyright file="MetaLearningEngine.Strategies.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
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
        _ = await baseModel.GetParametersAsync(ct).ConfigureAwait(false);

        for (var iteration = 0; iteration < config.MetaIterations; iteration++)
        {
            ct.ThrowIfCancellationRequested();

            var taskBatch = SampleTaskBatch(taskFamilies, config.TaskBatchSize);
            var metaGradients = new Dictionary<string, object>();

            foreach (var task in taskBatch)
            {
                var taskModel = await baseModel.CloneAsync(ct).ConfigureAwait(false);

                for (var innerStep = 0; innerStep < config.InnerSteps; innerStep++)
                {
                    var gradients = await taskModel.ComputeGradientsAsync(task.TrainingExamples, ct).ConfigureAwait(false);
                    await taskModel.UpdateParametersAsync(gradients, config.InnerLearningRate, ct).ConfigureAwait(false);
                }

                var metaGrad = await taskModel.ComputeGradientsAsync(task.ValidationExamples, ct).ConfigureAwait(false);

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
            await baseModel.UpdateParametersAsync(metaGradients, config.OuterLearningRate, ct).ConfigureAwait(false);
        }

        var finalParams = await baseModel.GetParametersAsync(ct).ConfigureAwait(false);
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
            var initialParams = await baseModel.GetParametersAsync(ct).ConfigureAwait(false);

            for (var innerStep = 0; innerStep < config.InnerSteps; innerStep++)
            {
                var gradients = await baseModel.ComputeGradientsAsync(task.TrainingExamples, ct).ConfigureAwait(false);
                await baseModel.UpdateParametersAsync(gradients, config.InnerLearningRate, ct).ConfigureAwait(false);
            }

            var adaptedParams = await baseModel.GetParametersAsync(ct).ConfigureAwait(false);
            var reptileGradients = ComputeReptileGradients(initialParams, adaptedParams);
            await baseModel.SetParametersAsync(initialParams, ct).ConfigureAwait(false);
            await baseModel.UpdateParametersAsync(reptileGradients, config.OuterLearningRate, ct).ConfigureAwait(false);
        }

        var finalParams = await baseModel.GetParametersAsync(ct).ConfigureAwait(false);
        return MetaModel.Create(baseModel, config, finalParams);
    }

}
