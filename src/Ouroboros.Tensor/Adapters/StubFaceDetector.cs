// <copyright file="StubFaceDetector.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Tensor.Abstractions;

namespace Ouroboros.Tensor.Adapters;

/// <summary>
/// Phase 237 stub <see cref="IFaceDetector"/>: returns a single full-frame
/// detection with confidence <c>1.0</c> and no landmarks. Used as the deterministic
/// fallback when the YuNet ONNX model file is absent, keeping the Phase 238 FER /
/// Phase 239 SFace consumers wired to a valid detector shape end-to-end without
/// a validated model. Mirrors <see cref="StubExpressionClassifier"/>.
/// </summary>
public sealed class StubFaceDetector : IFaceDetector
{
    private const int MinRgbaLength = 16;

    private readonly ILogger<StubFaceDetector>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StubFaceDetector"/> class.
    /// </summary>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public StubFaceDetector(ILogger<StubFaceDetector>? logger = null)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<FaceDetection>> DetectAsync(
        FrameBuffer frame,
        CancellationToken cancellationToken)
    {
        if (frame is null || frame.Rgba is null || frame.Rgba.Length < MinRgbaLength)
        {
            return Task.FromResult<IReadOnlyList<FaceDetection>>(Array.Empty<FaceDetection>());
        }

        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<FaceDetection> result =
        [
            new FaceDetection(
                X: 0,
                Y: 0,
                Width: frame.Width,
                Height: frame.Height,
                Confidence: 1.0f,
                Landmarks5: null),
        ];

        _logger?.LogTrace(
            "[StubFaceDetector] full-frame passthrough {W}x{H}",
            frame.Width,
            frame.Height);

        return Task.FromResult(result);
    }
}
