// Copyright (c) Ouroboros. All rights reserved.

using Ouroboros.Providers.HermesOnnx;

namespace Ouroboros.Providers.Tests.HermesOnnx;

public sealed class GenaiConfigRetargeterTests : IDisposable
{
    private readonly string _dir;
    private readonly string _configPath;

    public GenaiConfigRetargeterTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "hermes-retarget-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _configPath = Path.Combine(_dir, "genai_config.json");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
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
    public void LegacyCudaForm_RewrittenToDml_WithBackup()
    {
        File.WriteAllText(_configPath, """
        {
          "model": { "decoder": { "session_options": { "provider_options": [ { "cuda": { "enable_cuda_graph": "0" } } ] } } }
        }
        """);

        GenaiConfigRetargeter.EnsureDirectMlProvider(_dir);

        string after = File.ReadAllText(_configPath);
        after.Should().Contain("\"name\": \"DML\"");
        after.Should().NotContain("cuda");
        File.Exists(_configPath + ".cuda.bak").Should().BeTrue();
    }

    [Fact]
    public void NewFormNameCuda_RewrittenToDml_CaseInsensitive()
    {
        File.WriteAllText(_configPath, """
        {
          "model": { "decoder": { "session_options": { "provider_options": [ { "name": "CUDA", "options": [] } ] } } }
        }
        """);

        GenaiConfigRetargeter.EnsureDirectMlProvider(_dir);

        string after = File.ReadAllText(_configPath);
        after.Should().Contain("\"name\": \"DML\"");
    }

    [Fact]
    public void AlreadyDml_LeavesFileUnchanged_NoBackup()
    {
        string original = """
        {
          "model": { "decoder": { "session_options": { "provider_options": [ { "name": "DML", "options": [] } ] } } }
        }
        """;
        File.WriteAllText(_configPath, original);

        GenaiConfigRetargeter.EnsureDirectMlProvider(_dir);

        File.ReadAllText(_configPath).Should().Be(original);
        File.Exists(_configPath + ".cuda.bak").Should().BeFalse();
    }

    [Fact]
    public void SecondCall_NoOps()
    {
        File.WriteAllText(_configPath, """
        {
          "model": { "decoder": { "session_options": { "provider_options": [ { "cuda": {} } ] } } }
        }
        """);

        GenaiConfigRetargeter.EnsureDirectMlProvider(_dir);
        string firstWrite = File.ReadAllText(_configPath);

        GenaiConfigRetargeter.EnsureDirectMlProvider(_dir);

        File.ReadAllText(_configPath).Should().Be(firstWrite);
    }

    [Fact]
    public void MissingFile_ReturnsSilently()
    {
        // _configPath does not exist
        Action act = () => GenaiConfigRetargeter.EnsureDirectMlProvider(_dir);
        act.Should().NotThrow();
    }

    [Fact]
    public void PreservesSiblingFields()
    {
        File.WriteAllText(_configPath, """
        {
          "model": {
            "context_length": 524288,
            "decoder": { "session_options": { "provider_options": [ { "cuda": {} } ] } },
            "type": "llama"
          },
          "search": { "max_length": 524288 }
        }
        """);

        GenaiConfigRetargeter.EnsureDirectMlProvider(_dir);

        string after = File.ReadAllText(_configPath);
        after.Should().Contain("\"context_length\": 524288");
        after.Should().Contain("\"type\": \"llama\"");
        after.Should().Contain("\"max_length\": 524288");
    }
}
