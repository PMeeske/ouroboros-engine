using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Ouroboros.Providers.Tapo;

/// <summary>
/// Manages the lifecycle of the Python Tapo Gateway process.
/// Auto-starts on demand and provides health checking.
/// </summary>
public sealed class TapoGatewayManager : IAsyncDisposable
{
    private readonly ILogger<TapoGatewayManager>? _logger;
    private readonly string _gatewayScriptPath;
    private Process? _process;

    public TapoGatewayManager(string gatewayScriptPath, ILogger<TapoGatewayManager>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(gatewayScriptPath);
        _gatewayScriptPath = gatewayScriptPath;
        _logger = logger;
    }

    /// <summary>Gets the port the gateway is running on.</summary>
    public int Port { get; private set; }

    /// <summary>Gets the base URL of the running gateway.</summary>
    public string BaseUrl => $"http://127.0.0.1:{Port}";

    /// <summary>Gets whether the gateway process is running.</summary>
    public bool IsRunning => _process is { HasExited: false };

    /// <summary>
    /// Starts the Python Tapo Gateway process.
    /// </summary>
    public async Task<bool> StartAsync(
        string tapoUsername,
        string tapoPassword,
        string serverPassword,
        int port = 8123,
        string broadcastAddr = "192.168.1.255",
        CancellationToken ct = default)
    {
        if (IsRunning)
        {
            _logger?.LogWarning("Gateway already running on port {Port}", Port);
            return true;
        }

        Port = port;

        // Find Python executable
        var pythonPath = await FindPythonAsync(ct).ConfigureAwait(false);
        if (pythonPath == null)
        {
            _logger?.LogError("Python not found. Install Python 3.10+ to use the Tapo Gateway");
            return false;
        }

        // Verify gateway script exists
        if (!File.Exists(_gatewayScriptPath))
        {
            _logger?.LogError("Gateway script not found at {Path}", _gatewayScriptPath);
            return false;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = pythonPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add(_gatewayScriptPath);
        startInfo.ArgumentList.Add("--port");
        startInfo.ArgumentList.Add(port.ToString());
        startInfo.ArgumentList.Add("--tapo-username");
        startInfo.ArgumentList.Add(tapoUsername);
        startInfo.ArgumentList.Add("--tapo-password");
        startInfo.ArgumentList.Add(tapoPassword);
        startInfo.ArgumentList.Add("--server-password");
        startInfo.ArgumentList.Add(serverPassword);
        startInfo.ArgumentList.Add("--broadcast-addr");
        startInfo.ArgumentList.Add(broadcastAddr);

        try
        {
            // SECURITY: validated — ArgumentList prevents injection from credentials
            // and configuration parameters. UseShellExecute = false prevents shell interpretation.
            _process = Process.Start(startInfo);
            if (_process == null)
            {
                _logger?.LogError("Failed to start gateway process");
                return false;
            }

            // Forward process output to logger
            _process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                    _logger?.LogDebug("[Gateway] {Output}", e.Data);
            };
            _process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                    _logger?.LogDebug("[Gateway] {Error}", e.Data);
            };
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            _logger?.LogInformation("Gateway process started (PID {Pid}), waiting for health check...", _process.Id);

            // Wait for the gateway to become healthy
            var healthy = await WaitForHealthAsync(TimeSpan.FromSeconds(30), ct).ConfigureAwait(false);
            if (!healthy)
            {
                _logger?.LogError("Gateway failed health check after 30s");
                await StopAsync().ConfigureAwait(false);
                return false;
            }

            _logger?.LogInformation("Gateway ready on port {Port}", Port);
            return true;
        }
        catch (OperationCanceledException) { throw; }
        catch (InvalidOperationException ex)
        {
            _logger?.LogError(ex, "Failed to start gateway process");
            return false;
        }
    }

    /// <summary>
    /// Polls /health until the gateway responds or timeout is reached.
    /// </summary>
    public async Task<bool> WaitForHealthAsync(TimeSpan timeout, CancellationToken ct = default)
    {
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            if (_process is { HasExited: true })
            {
                _logger?.LogError("Gateway process exited prematurely (exit code {Code})", _process.ExitCode);
                return false;
            }

            try
            {
                var response = await httpClient.GetAsync($"{BaseUrl}/health", ct).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                    return true;
            }
            catch (HttpRequestException)
            {
                // Server not ready yet
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                // HTTP timeout - server not ready
            }

            await Task.Delay(500, ct).ConfigureAwait(false);
        }

        return false;
    }

    /// <summary>
    /// Stops the gateway process.
    /// </summary>
    public async Task StopAsync()
    {
        if (_process == null)
            return;

        try
        {
            if (!_process.HasExited)
            {
                _logger?.LogInformation("Stopping gateway process (PID {Pid})...", _process.Id);
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger?.LogWarning(ex, "Error stopping gateway process");
        }
        finally
        {
            _process.Dispose();
            _process = null;
        }
    }

    /// <summary>
    /// Finds a Python 3 executable on the system.
    /// </summary>
    private static async Task<string?> FindPythonAsync(CancellationToken ct)
    {
        // Try common Python executable names
        foreach (var name in new[] { "python", "python3" })
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = name,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                psi.ArgumentList.Add("--version");

                // SECURITY: safe — hardcoded python candidates with ArgumentList
                using var proc = Process.Start(psi);
                if (proc == null) continue;

                var output = await proc.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
                var error = await proc.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
                await proc.WaitForExitAsync(ct).ConfigureAwait(false);

                var version = (output + error).Trim();
                if (version.StartsWith("Python 3", StringComparison.OrdinalIgnoreCase))
                    return name;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // This executable name not found
            }
        }

        return null;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
    }
}
