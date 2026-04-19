// <copyright file="SharedOrtDmlSessionFactory.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Ouroboros.Tensor.Abstractions;
using Ouroboros.Tensor.Rasterizers;

namespace Ouroboros.Tensor.Backends;

/// <summary>
/// Production <see cref="ISharedOrtDmlSessionFactory"/>. Binds every ORT
/// DirectML session to the shared D3D12 device via LUID-matched
/// <c>deviceId</c> ordinal resolution. After this lands every direct
/// <c>SessionOptions.AppendExecutionProvider_DML(int)</c> call site in
/// <c>Ouroboros.Tensor</c> + the App avatar pipeline becomes a
/// code-review smell — the DML quirks
/// (<c>session.disable_mem_pattern=1</c>, <see cref="ExecutionMode.ORT_SEQUENTIAL"/>,
/// <see cref="GraphOptimizationLevel.ORT_ENABLE_ALL"/>) are centralized here
/// per Phase 196.3 RESEARCH §5.
/// </summary>
/// <remarks>
/// <para>
/// The ordinal is resolved exactly once via <see cref="Lazy{T}"/> and
/// logged at INFO ("DML EP bound to deviceId={Id} (LUID=0x{Luid:X16})")
/// so PIX + logs agree on the bound adapter.
/// </para>
/// <para>
/// C# ORT 1.24.4 exposes only <see cref="SessionOptions.AppendExecutionProvider_DML(int)"/>.
/// The external-device overload
/// (<c>OrtSessionOptionsAppendExecutionProviderEx_DML(IDMLDevice*, ID3D12CommandQueue*)</c>)
/// is not surfaced in managed bindings (onnxruntime#9164 / #4941). We
/// rely on D3D12's per-adapter singleton guarantee — if the ordinal
/// resolves to the same LUID <see cref="SharedD3D12Device"/> attached
/// to, ORT's internal <c>D3D12CreateDevice(deviceId)</c> returns the
/// same <c>ID3D12Device*</c>. No P/Invoke required.
/// </para>
/// </remarks>
public sealed class SharedOrtDmlSessionFactory : ISharedOrtDmlSessionFactory
{
    private readonly SharedD3D12Device _sharedDevice;
    private readonly Lazy<int> _deviceId;
    private readonly ILogger<SharedOrtDmlSessionFactory> _logger;
    private int _loggedBinding;

    /// <summary>
    /// Constructs the factory from the shared D3D12 device (Phase 188.1.1)
    /// and the DXGI LUID resolver.
    /// </summary>
    /// <param name="sharedDevice">Process-wide shared D3D12 device.</param>
    /// <param name="resolver">LUID → <c>EnumAdapters1</c> ordinal resolver.</param>
    /// <param name="logger">Diagnostic logger (non-null; use <see cref="Microsoft.Extensions.Logging.Abstractions.NullLogger{T}"/> in tests).</param>
    public SharedOrtDmlSessionFactory(
        SharedD3D12Device sharedDevice,
        DxgiAdapterLuidResolver resolver,
        ILogger<SharedOrtDmlSessionFactory> logger)
    {
        ArgumentNullException.ThrowIfNull(sharedDevice);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(logger);

        _sharedDevice = sharedDevice;
        _logger = logger;
        _deviceId = new Lazy<int>(() =>
            resolver.ResolveDmlDeviceIdForLuid(sharedDevice.ResolvedAdapterLuid));
    }

    /// <inheritdoc/>
    public SessionOptions CreateSessionOptions()
    {
        if (!_sharedDevice.IsAvailable)
        {
            throw new InvalidOperationException(
                "SharedD3D12Device is unavailable — caller must latch CPU execution provider fallback.");
        }

        int deviceId = _deviceId.Value;

        SessionOptions opts = new()
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
        };

        try
        {
            opts.AddSessionConfigEntry("session.disable_mem_pattern", "1");
            opts.AppendExecutionProvider_DML(deviceId);
        }
        catch
        {
            opts.Dispose();
            throw;
        }

        if (Interlocked.Exchange(ref _loggedBinding, 1) == 0)
        {
            _logger.LogInformation(
                "DML EP bound to deviceId={DeviceId} (LUID=0x{Luid:X16})",
                deviceId, _sharedDevice.ResolvedAdapterLuid);
        }

        return opts;
    }
}
