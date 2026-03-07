// <copyright file="OperatingCostAuditPrompts.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>


using Ouroboros.Pipeline.Prompts;

namespace Ouroboros.Pipeline.Reasoning;

/// <summary>
/// Prompt templates for Operating Cost Statement formal completeness audits.
/// These prompts enable non-legal structured completeness analysis of operating cost statements.
/// </summary>
public static class OperatingCostAuditPrompts
{
    /// <summary>
    /// System prompt for the operating cost audit module.
    /// Sets up the LLM to perform formal completeness checks without legal interpretation.
    /// </summary>
    public static readonly PromptTemplate SystemPrompt = new(
        PromptTemplateLoader.GetPromptText("Reasoning", "OperatingCostAuditSystem"));

    /// <summary>
    /// Prompt template for analyzing the main operating cost statement.
    /// Checks for the seven required minimum data points.
    /// </summary>
    public static readonly PromptTemplate AnalyzeMainStatement = new(
        PromptTemplateLoader.GetPromptText("Reasoning", "AnalyzeMainStatement"));

    /// <summary>
    /// Prompt template for comparing the main statement with HOA/WEG documents.
    /// </summary>
    public static readonly PromptTemplate CompareWithHoaStatement = new(
        PromptTemplateLoader.GetPromptText("Reasoning", "CompareWithHoaStatement"));

    /// <summary>
    /// Prompt template for checking allocation rules against rental agreement.
    /// </summary>
    public static readonly PromptTemplate CheckAllocationRules = new(
        PromptTemplateLoader.GetPromptText("Reasoning", "CheckAllocationRules"));

    /// <summary>
    /// Prompt template for generating the final audit report.
    /// </summary>
    public static readonly PromptTemplate GenerateAuditReport = new(
        PromptTemplateLoader.GetPromptText("Reasoning", "GenerateAuditReport"));

    /// <summary>
    /// Prompt template for extracting cost categories from documents.
    /// </summary>
    public static readonly PromptTemplate ExtractCostCategories = new(
        PromptTemplateLoader.GetPromptText("Reasoning", "ExtractCostCategories"));
}
