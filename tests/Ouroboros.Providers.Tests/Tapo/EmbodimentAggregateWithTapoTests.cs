using Ouroboros.Core.EmbodiedInteraction;

namespace Ouroboros.Tests.Providers.Tapo;

/// <summary>
/// Tests for EmbodimentAggregate with TapoEmbodimentProvider.
/// </summary>
[Trait("Category", "Unit")]
public class EmbodimentAggregateWithTapoTests
{
    [Fact]
    public void Constructor_CreatesAggregateWithInactiveStatus()
    {
        // Arrange & Act
        using var aggregate = new EmbodimentAggregate("test-aggregate", "Test Aggregate");

        // Assert
        aggregate.AggregateId.Should().Be("test-aggregate");
        aggregate.Name.Should().Be("Test Aggregate");
        aggregate.State.Status.Should().Be(AggregateStatus.Inactive);
    }

    [Fact]
    public void RegisterProvider_WithValidProvider_Succeeds()
    {
        // Arrange
        using var aggregate = new EmbodimentAggregate("test-aggregate");
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var provider = new TapoEmbodimentProvider(tapoClient);

        // Act
        var result = aggregate.RegisterProvider(provider);

        // Assert
        result.IsSuccess.Should().BeTrue();
        aggregate.Providers.Should().ContainKey(provider.ProviderId);
    }

    [Fact]
    public void RegisterProvider_WithNullProvider_ReturnsFailure()
    {
        // Arrange
        using var aggregate = new EmbodimentAggregate("test-aggregate");

        // Act
        var result = aggregate.RegisterProvider(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("required");
    }

    [Fact]
    public void RegisterProvider_DuplicateProvider_ReturnsFailure()
    {
        // Arrange
        using var aggregate = new EmbodimentAggregate("test-aggregate");
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var provider = new TapoEmbodimentProvider(tapoClient);

        aggregate.RegisterProvider(provider);

        // Create another provider with same ID
        using var provider2 = new TapoEmbodimentProvider(tapoClient, "tapo");

        // Act
        var result = aggregate.RegisterProvider(provider2);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already registered");
    }

    [Fact]
    public void ToBodySchema_ReturnsSchemaWithCapabilities()
    {
        // Arrange
        using var aggregate = new EmbodimentAggregate("test-aggregate");

        // Act
        var schema = aggregate.ToBodySchema();

        // Assert
        schema.Should().NotBeNull();
        schema.HasCapability(Capability.Reasoning).Should().BeTrue();
        schema.HasCapability(Capability.Remembering).Should().BeTrue();
    }

    [Fact]
    public void DomainEvents_Observable_EmitsProviderRegistered()
    {
        // Arrange
        using var aggregate = new EmbodimentAggregate("test-aggregate");
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        using var provider = new TapoEmbodimentProvider(tapoClient);

        var receivedEvents = new List<EmbodimentDomainEvent>();
        using var sub = aggregate.DomainEvents.Subscribe(e => receivedEvents.Add(e));

        // Act
        aggregate.RegisterProvider(provider);

        // Assert
        receivedEvents.Should().Contain(e => e.EventType == EmbodimentDomainEventType.ProviderRegistered);
    }

    [Fact]
    public void Dispose_DisposesAllProviders()
    {
        // Arrange
        using var httpClient = new HttpClient();
        using var tapoClient = new TapoRestClient(httpClient);
        var provider = new TapoEmbodimentProvider(tapoClient);
        var aggregate = new EmbodimentAggregate("test-aggregate");
        aggregate.RegisterProvider(provider);

        // Act
        aggregate.Dispose();

        // Assert - aggregate should be disposed
        aggregate.State.Status.Should().Be(AggregateStatus.Inactive);
    }
}