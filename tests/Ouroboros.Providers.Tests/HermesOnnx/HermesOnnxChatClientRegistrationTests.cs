// Copyright (c) Ouroboros. All rights reserved.

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ouroboros.Providers;

namespace Ouroboros.Providers.Tests.HermesOnnx;

public sealed class HermesOnnxChatClientRegistrationTests : IDisposable
{
    private readonly string _fakeModelDir;

    public HermesOnnxChatClientRegistrationTests()
    {
        _fakeModelDir = Path.Combine(Path.GetTempPath(), "hermes-fake-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_fakeModelDir);
        File.WriteAllText(Path.Combine(_fakeModelDir, "genai_config.json"), """
        { "model": { "decoder": { "session_options": { "provider_options": [ { "name": "DML", "options": [] } ] } } } }
        """);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_fakeModelDir, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup; ignore IO failures.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort cleanup; ignore permission failures.
        }
    }

    [Fact]
    public void NoRegistration_When_ModelPath_DoesNotExist()
    {
        ServiceCollection services = new();
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["HermesOnnx:ModelPath"] = "C:/this/path/does/not/exist/hermes",
        }).Build();

        services.AddHermesOnnxKeyedMeaiChatClient(config);

        using ServiceProvider sp = services.BuildServiceProvider();
        sp.GetKeyedService<IChatClient>("hermes-onnx").Should().BeNull();
    }

    [Fact]
    public void RegistersKeyedClient_When_ModelPath_Exists()
    {
        ServiceCollection services = new();
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["HermesOnnx:ModelPath"] = _fakeModelDir,
        }).Build();

        services.AddHermesOnnxKeyedMeaiChatClient(config);

        // The factory is registered, but resolving it would call new Model() which requires
        // a real ONNX file — that is a smoke-test concern (Plan 02). For Plan 01 we assert
        // the descriptor is present without resolving.
        services.Should().Contain(d =>
            d.ServiceType == typeof(IChatClient) &&
            object.Equals(d.ServiceKey, "hermes-onnx"));
    }
}
