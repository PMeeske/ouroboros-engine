// <copyright file="AgentResourceTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.MetaAI.SelfModel;

namespace Ouroboros.Agent.Tests.MetaAI.SelfModel;

[Trait("Category", "Unit")]
public class AgentResourceTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var name = "GPU Memory";
        var type = "Compute";
        var available = 8.0;
        var total = 16.0;
        var unit = "GB";
        var lastUpdated = DateTime.UtcNow;
        var metadata = new Dictionary<string, object> { ["model"] = "A100" };

        // Act
        var resource = new AgentResource(name, type, available, total, unit, lastUpdated, metadata);

        // Assert
        resource.Name.Should().Be(name);
        resource.Type.Should().Be(type);
        resource.Available.Should().Be(available);
        resource.Total.Should().Be(total);
        resource.Unit.Should().Be(unit);
        resource.LastUpdated.Should().Be(lastUpdated);
        resource.Metadata.Should().BeEquivalentTo(metadata);
    }

    [Fact]
    public void Constructor_WithEmptyMetadata_Succeeds()
    {
        var resource = new AgentResource(
            "CPU", "Compute", 4.0, 8.0, "cores",
            DateTime.UtcNow, new Dictionary<string, object>());

        resource.Metadata.Should().BeEmpty();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var time = DateTime.UtcNow;
        var meta = new Dictionary<string, object>();

        var a = new AgentResource("CPU", "Compute", 4.0, 8.0, "cores", time, meta);
        var b = new AgentResource("CPU", "Compute", 4.0, 8.0, "cores", time, meta);

        a.Should().Be(b);
    }

    [Fact]
    public void Equality_DifferentAvailable_AreNotEqual()
    {
        var time = DateTime.UtcNow;
        var meta = new Dictionary<string, object>();

        var a = new AgentResource("CPU", "Compute", 4.0, 8.0, "cores", time, meta);
        var b = new AgentResource("CPU", "Compute", 6.0, 8.0, "cores", time, meta);

        a.Should().NotBe(b);
    }

    [Fact]
    public void With_CanUpdateAvailable()
    {
        var original = new AgentResource(
            "Memory", "RAM", 8.0, 16.0, "GB",
            DateTime.UtcNow, new Dictionary<string, object>());

        var updated = original with { Available = 12.0 };

        updated.Available.Should().Be(12.0);
        updated.Total.Should().Be(original.Total);
    }

    [Fact]
    public void Constructor_AvailableCanExceedTotal()
    {
        // The record doesn't enforce this invariant, it's a data holder
        var resource = new AgentResource(
            "Test", "Test", 20.0, 10.0, "units",
            DateTime.UtcNow, new Dictionary<string, object>());

        resource.Available.Should().Be(20.0);
        resource.Total.Should().Be(10.0);
    }
}
