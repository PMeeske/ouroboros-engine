// <copyright file="LoggingTensorBackendTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;

namespace Ouroboros.Tests.Decorators;

[Trait("Category", "Unit")]
public sealed class LoggingTensorBackendTests
{
    [Fact]
    public void MatMul_Success_LogsDebugMessages()
    {
        // Arrange
        var messages = new List<string>();
        var logger = new CapturingLogger(messages);
        var backend = new LoggingTensorBackend(CpuTensorBackend.Instance, logger);

        using var a = CpuTensorBackend.Instance.Create(TensorShape.Of(2, 2), new float[4]);
        using var b = CpuTensorBackend.Instance.Create(TensorShape.Of(2, 2), new float[4]);

        // Act
        var result = backend.MatMul(a, b);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Dispose();
        messages.Should().Contain(m => m.Contains("MatMul") && m.Contains("[2, 2]"));
        messages.Should().Contain(m => m.Contains("completed"));
    }

    [Fact]
    public void Add_Failure_LogsWarning()
    {
        // Arrange
        var messages = new List<string>();
        var logger = new CapturingLogger(messages);
        var backend = new LoggingTensorBackend(CpuTensorBackend.Instance, logger);

        using var a = CpuTensorBackend.Instance.Create(TensorShape.Of(3), new float[3]);
        using var b = CpuTensorBackend.Instance.Create(TensorShape.Of(4), new float[4]);

        // Act
        var result = backend.Add(a, b);

        // Assert
        result.IsSuccess.Should().BeFalse();
        messages.Should().Contain(m => m.Contains("failed") || m.Contains("Add"));
    }

    [Fact]
    public void Device_DelegatesToInner()
    {
        var inner = Substitute.For<ITensorBackend>();
        inner.Device.Returns(DeviceType.Cuda);
        var logger = new CapturingLogger(new List<string>());

        var backend = new LoggingTensorBackend(inner, logger);

        backend.Device.Should().Be(DeviceType.Cuda);
    }

    // ── Capturing Logger Helper ───────────────────────────────────────────────

    private sealed class CapturingLogger : ILogger
    {
        private readonly List<string> _messages;
        public CapturingLogger(List<string> messages) => _messages = messages;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            _messages.Add($"[{logLevel}] {formatter(state, exception)}");
        }
    }
}
