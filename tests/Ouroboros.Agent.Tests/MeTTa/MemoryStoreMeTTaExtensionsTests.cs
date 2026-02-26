// <copyright file="MemoryStoreMeTTaExtensionsTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Abstractions;
using Ouroboros.Tools.MeTTa;

namespace Ouroboros.Tests.MeTTa;

[Trait("Category", "Unit")]
public class MemoryStoreMeTTaExtensionsTests
{
    private readonly Mock<IMeTTaEngine> _engineMock = new();
    private readonly MemoryStore _memoryStore = new();

    [Fact]
    public void CreateMeTTaBridge_ReturnsNonNullBridge()
    {
        var bridge = _memoryStore.CreateMeTTaBridge(_engineMock.Object);
        bridge.Should().NotBeNull();
    }

    [Fact]
    public async Task SyncToMeTTaAsync_DelegatesToBridgeSyncAll()
    {
        _engineMock
            .Setup(e => e.AddFactAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(Result<Unit, string>.Success(Unit.Value)));

        var result = await _memoryStore.SyncToMeTTaAsync(_engineMock.Object);

        result.IsSuccess.Should().BeTrue();
    }
}
