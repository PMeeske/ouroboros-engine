// <copyright file="SafetyGuardTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class SafetyGuardTests
{
    [Fact]
    public void Constructor_DefaultLevel_DoesNotThrow()
    {
        var act = () => new SafetyGuard();
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithLevel_DoesNotThrow()
    {
        var act = () => new SafetyGuard(PermissionLevel.Admin);
        act.Should().NotThrow();
    }

    [Fact]
    public async Task CheckActionSafetyAsync_NullActionName_Throws()
    {
        var guard = new SafetyGuard();
        var act = () => guard.CheckActionSafetyAsync(
            null!,
            new Dictionary<string, object>());
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CheckActionSafetyAsync_NullParameters_Throws()
    {
        var guard = new SafetyGuard();
        var act = () => guard.CheckActionSafetyAsync("test", null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CheckActionSafetyAsync_SafeAction_IsAllowed()
    {
        var guard = new SafetyGuard(PermissionLevel.Admin);
        var result = await guard.CheckActionSafetyAsync(
            "read",
            new Dictionary<string, object> { ["path"] = "/safe/path" });

        result.Should().NotBeNull();
    }
}
