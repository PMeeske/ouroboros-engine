// <copyright file="OllamaKeepAliveEvictorIntegrationTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Orchestration;

/// <summary>
/// Integration tests for <see cref="OllamaKeepAliveEvictor"/>.
/// These require a live Ollama instance at <c>http://localhost:11434</c>.
/// If Ollama is unavailable the tests are skipped at runtime.
/// </summary>
[Trait("Category", "Integration")]
public sealed class OllamaKeepAliveEvictorIntegrationTests : IDisposable
{
    private static readonly string Endpoint = "http://localhost:11434";
    private readonly HttpClient _pingClient = new() { Timeout = TimeSpan.FromSeconds(2) };

    private async Task<bool> IsOllamaAvailableAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var response = await _pingClient.GetAsync(Endpoint, cts.Token).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
    }

    [SkippableFact]
    public async Task EvictAsync_WithLiveOllama_DoesNotThrow()
    {
        Skip.IfNot(await IsOllamaAvailableAsync().ConfigureAwait(false), "Ollama not available at localhost:11434");

        using var evictor = new OllamaKeepAliveEvictor(
            tenantName: "Ollama-Test",
            endpoint: Endpoint,
            model: "llama3:latest",
            estimatedVramBytes: 4_000_000_000);

        evictor.CanEvictNow().Should().BeTrue();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var reclaimed = await evictor.EvictAsync(cts.Token).ConfigureAwait(false);

        reclaimed.Should().Be(4_000_000_000);
    }

    [SkippableFact]
    public async Task EvictAsync_WithoutModelName_ReturnsZero()
    {
        Skip.IfNot(await IsOllamaAvailableAsync().ConfigureAwait(false), "Ollama not available at localhost:11434");

        using var evictor = new OllamaKeepAliveEvictor(
            tenantName: "Ollama-NoModel",
            endpoint: Endpoint,
            model: null,
            estimatedVramBytes: 1_000_000_000);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var reclaimed = await evictor.EvictAsync(cts.Token).ConfigureAwait(false);

        reclaimed.Should().Be(0);
    }

    public void Dispose() => _pingClient.Dispose();
}
