using System.ComponentModel;
using Microsoft.SemanticKernel;
using Ouroboros.Tools;

namespace Ouroboros.SemanticKernel;

/// <summary>
/// Bridges the Ouroboros <see cref="ToolRegistry"/> to a Semantic Kernel <see cref="KernelPlugin"/>.
/// Each registered <see cref="ITool"/> becomes a <see cref="KernelFunction"/>.
/// </summary>
public static class ToolRegistryPluginBridge
{
    /// <summary>
    /// Converts all tools in the registry to a single <see cref="KernelPlugin"/>
    /// named "OuroborosTools".
    /// </summary>
    public static KernelPlugin ToKernelPlugin(
        ToolRegistry registry,
        string pluginName = "OuroborosTools")
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginName);

        var functions = new List<KernelFunction>();
        foreach (ITool tool in registry.All)
        {
            functions.Add(CreateKernelFunction(tool));
        }

        return KernelPluginFactory.CreateFromFunctions(pluginName, functions);
    }

    private static KernelFunction CreateKernelFunction(ITool tool)
    {
        return KernelFunctionFactory.CreateFromMethod(
            async (string input, CancellationToken ct) =>
            {
                var result = await tool.InvokeAsync(input, ct).ConfigureAwait(false);
                return result.IsSuccess ? result.Value : $"Error: {result.Error}";
            },
            functionName: SanitizeFunctionName(tool.Name),
            description: tool.Description);
    }

    /// <summary>
    /// SK function names must be valid C# identifiers. Replace invalid characters.
    /// </summary>
    private static string SanitizeFunctionName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "UnnamedTool";

        var sb = new System.Text.StringBuilder(name.Length);
        foreach (char c in name)
        {
            sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
        }

        string result = sb.ToString();
        return char.IsDigit(result[0]) ? $"Tool_{result}" : result;
    }
}
