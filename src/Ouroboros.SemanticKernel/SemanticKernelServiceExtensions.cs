using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.SemanticKernel;
using Ouroboros.Abstractions.Core;
using Ouroboros.Providers.Meai;

namespace Ouroboros.SemanticKernel;

/// <summary>
/// DI extensions for registering Semantic Kernel services in the Ouroboros container.
/// </summary>
public static class SemanticKernelServiceExtensions
{
    /// <summary>
    /// Registers a Semantic Kernel <see cref="Kernel"/> as a singleton,
    /// backed by the already-registered <see cref="IChatCompletionModel"/>
    /// and optional <see cref="Ouroboros.Tools.ToolRegistry"/>.
    /// </summary>
    public static IServiceCollection AddSemanticKernel(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(sp =>
        {
            // Prefer IChatClient if already registered (e.g. via AddMeaiChatClient)
            IChatClient? chatClient = sp.GetService<IChatClient>();
            Ouroboros.Tools.ToolRegistry? tools = sp.GetService<Ouroboros.Tools.ToolRegistry>();

            if (chatClient is not null)
            {
                return KernelFactory.CreateKernel(chatClient, tools);
            }

            // Fall back to IChatCompletionModel
            var model = sp.GetRequiredService<IChatCompletionModel>();
            return KernelFactory.CreateKernel(model, tools);
        });

        return services;
    }
}
