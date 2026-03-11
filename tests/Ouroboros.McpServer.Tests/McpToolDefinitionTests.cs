using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Ouroboros.McpServer.Tests;

[Trait("Category", "Unit")]
public class McpToolDefinitionTests
{
    [Fact]
    public void Constructor_WithRequiredName_SetsName()
    {
        var definition = new McpToolDefinition { Name = "test-tool" };

        definition.Name.Should().Be("test-tool");
    }

    [Fact]
    public void Description_WhenNotSet_IsNull()
    {
        var definition = new McpToolDefinition { Name = "tool" };

        definition.Description.Should().BeNull();
    }

    [Fact]
    public void Description_WhenSet_ReturnsValue()
    {
        var definition = new McpToolDefinition
        {
            Name = "tool",
            Description = "A test tool",
        };

        definition.Description.Should().Be("A test tool");
    }

    [Fact]
    public void InputSchema_WhenNotSet_IsNull()
    {
        var definition = new McpToolDefinition { Name = "tool" };

        definition.InputSchema.Should().BeNull();
    }

    [Fact]
    public void InputSchema_WhenSet_ReturnsJsonElement()
    {
        var schema = JsonSerializer.Deserialize<JsonElement>("""{"type":"object"}""");
        var definition = new McpToolDefinition
        {
            Name = "tool",
            InputSchema = schema,
        };

        definition.InputSchema.ValueKind.Should().NotBe(System.Text.Json.JsonValueKind.Undefined);
        definition.InputSchema.GetProperty("type").GetString().Should().Be("object");
    }
}
