using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Ouroboros.Interop.Hosting;

/// <summary>
/// Pure Step wrappers for HostApplicationBuilder and ConfigurationManager to enable | pipe composition.
/// </summary>
public static class HostStepExtensions
{
    // Use(HostApplicationBuilder -> HostApplicationBuilder)
    public static Step<HostApplicationBuilder, HostApplicationBuilder> Use(
        Func<HostApplicationBuilder, HostApplicationBuilder> configure)
        => new(async b => await Task.FromResult(configure(b)).ConfigureAwait(false));

    // Use(ConfigurationManager -> ConfigurationManager)
    public static Step<ConfigurationManager, ConfigurationManager> Use(
        Func<ConfigurationManager, ConfigurationManager> configure)
        => new(async c => await Task.FromResult(configure(c)).ConfigureAwait(false));

    /// <summary>
    /// Register services inside HostApplicationBuilder via a pipeline step.
    /// </summary>
    /// <returns></returns>
    public static Step<HostApplicationBuilder, HostApplicationBuilder> Services(Action<IServiceCollection> configure)
        => new(async b =>
        {
            configure(b.Services);
            return await Task.FromResult(b).ConfigureAwait(false);
        });

    /// <summary>
    /// Convenience to add interchangeable LLM (OpenAI via reflection or Ollama) to the service collection.
    /// </summary>
    /// <returns></returns>
    public static Step<HostApplicationBuilder, HostApplicationBuilder> AddInterchangeableLlm(string? model = null, string? embed = null)
        => Services(s => s.AddInterchangeableLlm(model, embed));
}
