namespace Ouroboros.Tests.EmbodiedInteraction;

/// <summary>
/// Tests for BodySchema.
/// </summary>
[Trait("Category", "Unit")]
public class BodySchemaTests
{
    [Fact]
    public void BodySchema_EmptyByDefault()
    {
        // Arrange & Act
        var schema = new BodySchema();

        // Assert
        Assert.Empty(schema.Sensors);
        Assert.Empty(schema.Actuators);
        Assert.Empty(schema.Capabilities);
        Assert.Empty(schema.Limitations);
    }

    [Fact]
    public void BodySchema_WithSensor_AddsImmutably()
    {
        // Arrange
        var schema = new BodySchema();
        var sensor = SensorDescriptor.Audio("mic1", "Microphone");

        // Act
        var newSchema = schema.WithSensor(sensor);

        // Assert
        Assert.Empty(schema.Sensors); // Original unchanged
        Assert.Single(newSchema.Sensors);
        Assert.Contains(Capability.Hearing, newSchema.Capabilities);
    }

    [Fact]
    public void BodySchema_WithActuator_AddsImmutably()
    {
        // Arrange
        var schema = new BodySchema();
        var actuator = ActuatorDescriptor.Voice("voice1", "Voice Output");

        // Act
        var newSchema = schema.WithActuator(actuator);

        // Assert
        Assert.Empty(schema.Actuators); // Original unchanged
        Assert.Single(newSchema.Actuators);
        Assert.Contains(Capability.Speaking, newSchema.Capabilities);
    }

    [Fact]
    public void BodySchema_WithLimitation_AddsImmutably()
    {
        // Arrange
        var schema = new BodySchema();
        var limitation = new Limitation(
            LimitationType.MemoryBounded,
            "Limited context window",
            0.7);

        // Act
        var newSchema = schema.WithLimitation(limitation);

        // Assert
        Assert.Empty(schema.Limitations); // Original unchanged
        Assert.Single(newSchema.Limitations);
        Assert.Equal("Limited context window", newSchema.Limitations[0].Description);
    }

    [Fact]
    public void BodySchema_CreateConversational_HasCorrectDefaults()
    {
        // Arrange & Act
        var schema = BodySchema.CreateConversational();

        // Assert
        Assert.Contains(Capability.Reading, schema.Capabilities);
        Assert.Contains(Capability.Writing, schema.Capabilities);
        Assert.Contains(Capability.Reasoning, schema.Capabilities);
        Assert.NotEmpty(schema.Limitations);
    }

    [Fact]
    public void BodySchema_CreateMultimodal_HasAllModalities()
    {
        // Arrange & Act
        var schema = BodySchema.CreateMultimodal();

        // Assert
        Assert.Contains(Capability.Hearing, schema.Capabilities);
        Assert.Contains(Capability.Seeing, schema.Capabilities);
        Assert.Contains(Capability.Speaking, schema.Capabilities);
        Assert.True(schema.GetSensorsByModality(SensorModality.Audio).Any());
        Assert.True(schema.GetSensorsByModality(SensorModality.Visual).Any());
        Assert.True(schema.GetActuatorsByModality(ActuatorModality.Voice).Any());
    }

    [Fact]
    public void BodySchema_DescribeSelf_GeneratesDescription()
    {
        // Arrange
        var schema = BodySchema.CreateMultimodal();

        // Act
        var description = schema.DescribeSelf();

        // Assert
        Assert.Contains("Sensors", description);
        Assert.Contains("Actuators", description);
        Assert.Contains("Capabilities", description);
        Assert.Contains("Limitations", description);
    }

    [Fact]
    public void BodySchema_WithoutSensor_RemovesImmutably()
    {
        // Arrange
        var schema = new BodySchema()
            .WithSensor(SensorDescriptor.Audio("mic1", "Microphone"))
            .WithSensor(SensorDescriptor.Visual("cam1", "Camera"));

        // Act
        var newSchema = schema.WithoutSensor("mic1");

        // Assert
        Assert.Equal(2, schema.Sensors.Count); // Original unchanged
        Assert.Single(newSchema.Sensors);
        Assert.False(newSchema.Sensors.ContainsKey("mic1"));
    }

    [Fact]
    public void BodySchema_GetSensor_ReturnsOption()
    {
        // Arrange
        var schema = new BodySchema()
            .WithSensor(SensorDescriptor.Audio("mic1", "Microphone"));

        // Act
        var found = schema.GetSensor("mic1");
        var notFound = schema.GetSensor("nonexistent");

        // Assert
        Assert.True(found.HasValue);
        Assert.False(notFound.HasValue);
    }

    [Fact]
    public void BodySchema_HasCapability_ReturnsCorrectly()
    {
        // Arrange
        var schema = BodySchema.CreateMultimodal();

        // Act & Assert
        Assert.True(schema.HasCapability(Capability.Hearing));
        Assert.True(schema.HasCapability(Capability.Seeing));
        Assert.True(schema.HasCapability(Capability.Speaking));
    }
}