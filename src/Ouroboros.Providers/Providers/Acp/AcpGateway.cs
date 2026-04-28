// <copyright file="AcpGateway.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace Ouroboros.Providers.Acp;

/// <summary>
/// Agent Communication Protocol (ACP) gateway for Ouroboros.
/// Enables Ouroboros agents to delegate tasks to Hermes Agent and other
/// ACP-compatible AI coding agents via stdio subprocesses.
/// </summary>
/// <remarks>
/// ACP is a protocol for spawning autonomous coding agents (Claude Code, Codex, Hermes,
/// OpenCode) as subprocesses and communicating with them via JSON-RPC over stdin/stdout.
/// Each spawned agent runs in an isolated process with its own tools, memory, and session.
/// <para>
/// The <see cref="AcpGateway"/> handles:
/// <list type="bullet">
/// <item>Spawning agent processes (hermes, claude, codex, opencode)</item>
/// <item>JSON-RPC request/response exchange</item>
/// <item>Structured input/output with tool context passing</item>
/// <item>Graceful shutdown and timeout handling</item>
/// </list>
/// </para>
/// <para>
/// Usage pattern:
/// <code>
/// var gateway = new AcpGateway("hermes", "/usr/local/bin/hermes");
/// await gateway.InitializeAsync();
/// var result = await gateway.SendTaskAsync("Build a FastAPI auth service", TimeSpan.FromMinutes(5));
/// Console.WriteLine(result.Stdout);
/// gateway.Dispose();
/// </code>
/// </para>
/// </remarks>
public sealed class AcpGateway : IDisposable
{
    private readonly string _agentName;
    private readonly string _command;
    private readonly IReadOnlyList<string> _args;
    private readonly IReadOnlyDictionary<string, string> _env;
    private readonly ILogger? _logger;
    private readonly TimeSpan _defaultTimeout;

    private Process? _process;
    private StreamWriter? _writer;
    private StreamReader? _reader;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private int _requestId;
    private bool _disposed;

    /// <summary>
    /// Creates an ACP gateway for the specified agent.
    /// </summary>
    /// <param name="agentName">Human-readable name for this agent connection.</param>
    /// <param name="command">Path to the agent CLI executable.</param>
    /// <param name="args">Additional arguments passed to the agent CLI.</param>
    /// <param name="env">Additional environment variables.</param>
    /// <param name="defaultTimeout">Default timeout for task execution.</param>
    /// <param name="logger">Optional logger.</param>
    public AcpGateway(
        string agentName,
        string command,
        IReadOnlyList<string>? args = null,
        IReadOnlyDictionary<string, string>? env = null,
        TimeSpan? defaultTimeout = null,
        ILogger? logger = null)
    {
        _agentName = agentName;
        _command = command;
        _args = args ?? ["--acp", "--stdio"]; // ACP stdio mode is default
        _env = env ?? new Dictionary<string, string>();
        _defaultTimeout = defaultTimeout ?? TimeSpan.FromMinutes(10);
        _logger = logger;
    }

    /// <summary>
    /// Spawns the agent process and waits for it to become ready.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_process is { HasExited: false })
        {
            _logger?.LogWarning("ACP gateway '{Agent}' already initialized", _agentName);
            return;
        }

        var psi = new ProcessStartInfo
        {
            FileName = _command,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (string arg in _args)
        {
            psi.ArgumentList.Add(arg);
        }

        foreach (var (key, value) in _env)
        {
            psi.Environment[key] = value;
        }

        _process = Process.Start(psi);
        if (_process is null)
        {
            throw new InvalidOperationException($"Failed to start ACP agent: {_command}");
        }

        _writer = new StreamWriter(_process.StandardInput.BaseStream, Encoding.UTF8);
        _reader = new StreamReader(_process.StandardOutput.BaseStream, Encoding.UTF8);

        // Drain startup banner
        await Task.Delay(1000, ct).ConfigureAwait(false);
        await DrainStartupAsync(ct).ConfigureAwait(false);

        // Send ACP initialize request
        JsonObject initRequest = new()
        {
            ["jsonrpc"] = "2.0",
            ["id"] = GetNextId(),
            ["method"] = "initialize",
            ["params"] = new JsonObject
            {
                ["protocolVersion"] = "2024-11-05",
                ["clientInfo"] = new JsonObject
                {
                    ["name"] = "ouroboros-acp-client",
                    ["version"] = "1.0.0",
                },
            },
        };

        JsonNode? response = await SendRequestAsync(initRequest, TimeSpan.FromSeconds(30), ct)
            .ConfigureAwait(false);

        if (response?["error"] is JsonObject error)
        {
            throw new InvalidOperationException($"ACP initialize failed for '{_agentName}': {error}");
        }

        _logger?.LogInformation("ACP gateway '{Agent}' initialized (PID {Pid})", _agentName, _process.Id);
    }

    /// <summary>
    /// Sends a task to the agent and awaits completion.
    /// </summary>
    /// <param name="taskDescription">Natural language description of the task.</param>
    /// <param name="timeout">Override timeout for this task.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Task result containing stdout, exit code, and file outputs.</returns>
    public async Task<AcpTaskResult> SendTaskAsync(
        string taskDescription,
        TimeSpan? timeout = null,
        CancellationToken ct = default)
    {
        EnsureInitialized();

        var taskRequest = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = GetNextId(),
            ["method"] = "tasks/send",
            ["params"] = new JsonObject
            {
                ["message"] = new JsonObject
                {
                    ["role"] = "user",
                    ["parts"] = new JsonArray(new JsonObject
                    {
                        ["type"] = "text",
                        ["text"] = taskDescription,
                    }),
                },
            },
        };

        TimeSpan actualTimeout = timeout ?? _defaultTimeout;
        JsonNode? response = await SendRequestAsync(taskRequest, actualTimeout, ct)
            .ConfigureAwait(false);

        if (response is null)
        {
            return new AcpTaskResult(string.Empty, -1, [], "No response from agent");
        }

        if (response["error"] is JsonObject error)
        {
            string errorMsg = error["message"]?.GetValue<string>() ?? error.ToJsonString();
            return new AcpTaskResult(string.Empty, -1, [], errorMsg);
        }

        return ParseTaskResult(response);
    }

    /// <summary>
    /// Sends a task to the agent without waiting for completion (fire-and-forget).
    /// Useful for long-running background tasks.
    /// </summary>
    public async Task SendTaskFireAndForgetAsync(string taskDescription, CancellationToken ct = default)
    {
        EnsureInitialized();

        var notification = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["method"] = "tasks/send",
            ["params"] = new JsonObject
            {
                ["message"] = new JsonObject
                {
                    ["role"] = "user",
                    ["parts"] = new JsonArray(new JsonObject
                    {
                        ["type"] = "text",
                        ["text"] = taskDescription,
                    }),
                },
            },
        };

        await SendNotificationAsync(notification, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Checks if the agent process is still alive.
    /// </summary>
    public bool IsAlive => _process is { HasExited: false };

    /// <summary>
    /// Returns the agent process ID, or null if not initialized.
    /// </summary>
    public int? ProcessId => _process?.Id;

    /// <summary>
    /// Gracefully shuts down the agent subprocess.
    /// </summary>
    public async Task ShutdownAsync(CancellationToken ct = default)
    {
        if (_process is null || _process.HasExited)
        {
            return;
        }

        try
        {
            var shutdownRequest = new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = GetNextId(),
                ["method"] = "shutdown",
            };

            await SendRequestAsync(shutdownRequest, TimeSpan.FromSeconds(10), ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or InvalidOperationException)
        {
            // Best-effort graceful shutdown
        }

        await TerminateProcessAsync(ct).ConfigureAwait(false);
    }

    // ── Internal request/response ──────────────────────────────────────────────

    private async Task<JsonNode?> SendRequestAsync(JsonObject request, TimeSpan timeout, CancellationToken ct)
    {
        if (_writer is null || _reader is null)
        {
            throw new InvalidOperationException("ACP gateway not initialized");
        }

        string json = request.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);

        await _lock.WaitAsync(cts.Token).ConfigureAwait(false);
        try
        {
            await _writer.WriteLineAsync(json).ConfigureAwait(false);
            await _writer.FlushAsync(cts.Token).ConfigureAwait(false);

            // Read response with timeout
            string? responseLine = await _reader.ReadLineAsync(cts.Token).ConfigureAwait(false);
            if (string.IsNullOrEmpty(responseLine))
            {
                return null;
            }

            return JsonNode.Parse(responseLine);
        }
        finally
        {
            _lock.Release();
            cts.Dispose();
        }
    }

    private async Task SendNotificationAsync(JsonObject notification, CancellationToken ct)
    {
        if (_writer is null)
        {
            throw new InvalidOperationException("ACP gateway not initialized");
        }

        string json = notification.ToJsonString();
        await _writer.WriteLineAsync(json).ConfigureAwait(false);
        await _writer.FlushAsync(ct).ConfigureAwait(false);
    }

    private void EnsureInitialized()
    {
        if (_process is null || _process.HasExited)
        {
            throw new InvalidOperationException($"ACP gateway '{_agentName}' not initialized or process exited");
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private async Task DrainStartupAsync(CancellationToken ct)
    {
        if (_reader is null) return;

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, cts.Token);

            while (!linked.Token.IsCancellationRequested)
            {
                Task<string?> readTask = _reader.ReadLineAsync(linked.Token).AsTask();
                if (await Task.WhenAny(readTask, Task.Delay(100, linked.Token)).ConfigureAwait(false) == readTask)
                {
                    string? line = await readTask.ConfigureAwait(false);
                    if (line is null) break;
                    _logger?.LogTrace("ACP startup line: {Line}", line);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected — startup banner drained or timed out
        }
    }

    private async Task TerminateProcessAsync(CancellationToken ct)
    {
        if (_process is null) return;

        try
        {
            if (!_process.HasExited)
            {
                _process.Kill();
                await _process.WaitForExitAsync(ct).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or System.ComponentModel.Win32Exception)
        {
            _logger?.LogError(ex, "Error terminating ACP process {Pid}", _process.Id);
        }
        finally
        {
            _process.Dispose();
            _process = null;
        }
    }

    private static AcpTaskResult ParseTaskResult(JsonNode response)
    {
        if (response["result"] is not JsonObject result)
        {
            return new AcpTaskResult(string.Empty, -1, [], "Missing result in response");
        }

        string? stdout = result["stdout"]?.GetValue<string>();
        int? exitCode = result["exitCode"]?.GetValue<int>();

        var artifacts = new List<AcpArtifact>();
        if (result["artifacts"] is JsonArray artifactArray)
        {
            foreach (JsonNode? artifact in artifactArray)
            {
                if (artifact is null) continue;

                artifacts.Add(new AcpArtifact(
                    artifact["name"]?.GetValue<string>() ?? "unknown",
                    artifact["path"]?.GetValue<string>() ?? string.Empty,
                    artifact["content"]?.GetValue<string>()));
            }
        }

        return new AcpTaskResult(
            stdout ?? string.Empty,
            exitCode ?? 0,
            artifacts,
            null);
    }

    private int GetNextId() => Interlocked.Increment(ref _requestId);

    // ── IDisposable ─────────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            _writer?.Dispose();
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException) { }

        try
        {
            _reader?.Dispose();
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException) { }

        try
        {
            if (_process is { HasExited: false })
            {
                _process.Kill();
                _process.WaitForExit(TimeSpan.FromSeconds(5));
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or System.ComponentModel.Win32Exception) { }

        _process?.Dispose();
        _lock.Dispose();

        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Result of an ACP task execution.
/// </summary>
public sealed record AcpTaskResult(
    string Stdout,
    int ExitCode,
    IReadOnlyList<AcpArtifact> Artifacts,
    string? ErrorMessage)
{
    /// <summary>
    /// True if the task completed successfully (exit code 0 and no error message).
    /// </summary>
    public bool IsSuccess => ExitCode == 0 && string.IsNullOrEmpty(ErrorMessage);
}

/// <summary>
/// A file artifact produced by an ACP agent.
/// </summary>
public sealed record AcpArtifact(
    string Name,
    string Path,
    string? Content);
