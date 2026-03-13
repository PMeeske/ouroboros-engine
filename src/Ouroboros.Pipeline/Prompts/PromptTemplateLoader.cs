// <copyright file="PromptTemplateLoader.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.Prompts;

using System.Collections.Concurrent;
using System.Reflection;

/// <summary>
/// Loads prompt templates from embedded YAML resource files.
/// Uses a simple parser to extract the template field without requiring a YAML library dependency.
/// </summary>
public static class PromptTemplateLoader
{
    private static readonly ConcurrentDictionary<string, string> RawCache = new();
    private static readonly ConcurrentDictionary<string, string> TemplateCache = new();

    /// <summary>
    /// Loads the raw YAML content of a prompt template from an embedded resource.
    /// </summary>
    /// <param name="category">The prompt category (e.g., "Council", "Reasoning").</param>
    /// <param name="name">The prompt name without extension (e.g., "Pragmatist").</param>
    /// <returns>The raw YAML content of the prompt template.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the embedded resource is not found.</exception>
    public static string LoadTemplate(string category, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        string cacheKey = $"{category}.{name}";
        return RawCache.GetOrAdd(cacheKey, _ =>
        {
            Assembly assembly = typeof(PromptTemplateLoader).Assembly;
            string resourceName = $"Ouroboros.Prompts.{category}.{name}.yaml";

            using Stream? stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                throw new FileNotFoundException(
                    $"Prompt template not found: {resourceName}. " +
                    $"Available resources: {string.Join(", ", assembly.GetManifestResourceNames())}");
            }

            using StreamReader reader = new(stream);
            return reader.ReadToEnd();
        });
    }

    /// <summary>
    /// Extracts and returns the prompt text from a YAML template's "template: |" block.
    /// </summary>
    /// <param name="category">The prompt category (e.g., "Council", "Reasoning").</param>
    /// <param name="name">The prompt name without extension (e.g., "Pragmatist").</param>
    /// <returns>The extracted prompt text with leading/trailing whitespace trimmed.</returns>
    public static string GetPromptText(string category, string name)
    {
        string cacheKey = $"{category}.{name}";
        return TemplateCache.GetOrAdd(cacheKey, _ =>
        {
            string yaml = LoadTemplate(category, name);
            return ExtractTemplateBlock(yaml);
        });
    }

    /// <summary>
    /// Extracts the "template: |" block from a YAML string using simple line-based parsing.
    /// Handles YAML literal block scalar (|) syntax: reads all indented lines following the marker.
    /// </summary>
    /// <param name="yaml">The raw YAML content.</param>
    /// <returns>The extracted template text.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the template block cannot be found.</exception>
    internal static string ExtractTemplateBlock(string yaml)
    {
        string[] lines = yaml.Split('\n');
        int templateLineIndex = -1;

        // Find the "template: |" or "template: |" line
        for (int i = 0; i < lines.Length; i++)
        {
            string trimmed = lines[i].TrimEnd('\r').Trim();
            if (trimmed is "template: |" or "template: |+")
            {
                templateLineIndex = i;
                break;
            }
        }

        if (templateLineIndex < 0)
        {
            throw new InvalidOperationException(
                "Could not find 'template: |' block in YAML content.");
        }

        // Determine the indentation level of the block content
        // (first non-empty line after "template: |")
        int contentIndent = -1;
        List<string> contentLines = [];

        for (int i = templateLineIndex + 1; i < lines.Length; i++)
        {
            string line = lines[i].TrimEnd('\r');

            // Empty lines are preserved within the block
            if (string.IsNullOrWhiteSpace(line))
            {
                // Only add if we've started capturing content
                if (contentIndent >= 0)
                {
                    contentLines.Add(string.Empty);
                }

                continue;
            }

            // Measure leading whitespace
            int indent = 0;
            while (indent < line.Length && line[indent] == ' ')
            {
                indent++;
            }

            // First content line establishes the indent level
            if (contentIndent < 0)
            {
                contentIndent = indent;
            }

            // If this line has less indentation than the block, we've left the block
            if (indent < contentIndent)
            {
                break;
            }

            // Strip the block indentation prefix
            contentLines.Add(line[contentIndent..]);
        }

        // Trim trailing empty lines
        while (contentLines.Count > 0 && string.IsNullOrWhiteSpace(contentLines[^1]))
        {
            contentLines.RemoveAt(contentLines.Count - 1);
        }

        return string.Join("\n", contentLines);
    }

    /// <summary>
    /// Clears the internal cache. Intended for testing scenarios.
    /// </summary>
    internal static void ClearCache()
    {
        RawCache.Clear();
        TemplateCache.Clear();
    }
}
