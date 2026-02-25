// <copyright file="ToolSelectorTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using IChatCompletionModel = Ouroboros.Abstractions.Core.IChatCompletionModel;

namespace Ouroboros.Agent.Tests.MetaAI;

/// <summary>
/// Tests for ToolSelector functionality.
/// </summary>
[Trait("Category", "Unit")]
public class ToolSelectorTests
{
    /// <summary>
    /// Mock chat model that returns predefined responses.
    /// </summary>
    private class MockChatCompletionModel : IChatCompletionModel
    {
        private readonly Func<string, CancellationToken, Task<string>> _generateFunc;

        public MockChatCompletionModel(Func<string, CancellationToken, Task<string>>? generateFunc = null)
        {
            _generateFunc = generateFunc ?? ((prompt, ct) => Task.FromResult(@"{""tool"": null}"));
        }

        public Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
            => _generateFunc(prompt, ct);
    }

    /// <summary>
    /// Mock tool for testing.
    /// </summary>
    private class MockTool : ITool
    {
        public string Name { get; }
        public string Description { get; }
        public string? JsonSchema { get; }

        public MockTool(string name, string description, string? jsonSchema = null)
        {
            Name = name;
            Description = description;
            JsonSchema = jsonSchema;
        }

        public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct)
            => Task.FromResult(Result<string, string>.Success($"Executed {Name} with input: {input}"));
    }

    [Fact]
    public async Task SelectToolAsync_WithNoTools_ReturnsNull()
    {
        // Arrange
        var llm = new MockChatCompletionModel();
        var selector = new Ouroboros.Agent.MetaAI.ToolSelector(new List<ITool>(), llm);

        // Act
        var result = await selector.SelectToolAsync("Search for documents", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SelectToolAsync_WithEmptyStep_ReturnsNull()
    {
        // Arrange
        var tools = new List<ITool> { new MockTool("SearchTool", "Searches documents") };
        var llm = new MockChatCompletionModel();
        var selector = new Ouroboros.Agent.MetaAI.ToolSelector(tools, llm);

        // Act
        var result = await selector.SelectToolAsync("", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SelectToolAsync_LLMReturnsNoTool_ReturnsNull()
    {
        // Arrange
        var tools = new List<ITool> { new MockTool("SearchTool", "Searches documents") };
        var llm = new MockChatCompletionModel((_, __) => Task.FromResult(@"{""tool"": null}"));
        var selector = new Ouroboros.Agent.MetaAI.ToolSelector(tools, llm);

        // Act
        var result = await selector.SelectToolAsync("Just think about this", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SelectToolAsync_LLMSelectsTool_ReturnsToolSelection()
    {
        // Arrange
        var tools = new List<ITool> 
        { 
            new MockTool("DatabaseQueryTool", "Queries the database", @"{""query"": ""string""}")
        };
        var llm = new MockChatCompletionModel((_, __) => Task.FromResult(
            @"{""tool"": ""DatabaseQueryTool"", ""arguments"": {""query"": ""SELECT * FROM users""}}"));
        var selector = new Ouroboros.Agent.MetaAI.ToolSelector(tools, llm);

        // Act
        var result = await selector.SelectToolAsync("Search the database for all users", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.ToolName.Should().Be("DatabaseQueryTool");
        result.ArgumentsJson.Should().Contain("SELECT * FROM users");
    }

    [Fact]
    public async Task SelectToolAsync_LLMSelectsToolWithoutArguments_ReturnsToolSelectionWithEmptyArgs()
    {
        // Arrange
        var tools = new List<ITool> { new MockTool("RefreshTool", "Refreshes the cache") };
        var llm = new MockChatCompletionModel((_, __) => Task.FromResult(@"{""tool"": ""RefreshTool""}"));
        var selector = new Ouroboros.Agent.MetaAI.ToolSelector(tools, llm);

        // Act
        var result = await selector.SelectToolAsync("Refresh the cache", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.ToolName.Should().Be("RefreshTool");
        result.ArgumentsJson.Should().Be("{}");
    }

    [Fact]
    public async Task SelectToolAsync_LLMReturnsInvalidJson_ReturnsNull()
    {
        // Arrange
        var tools = new List<ITool> { new MockTool("SearchTool", "Searches documents") };
        var llm = new MockChatCompletionModel((_, __) => Task.FromResult("This is not valid JSON"));
        var selector = new Ouroboros.Agent.MetaAI.ToolSelector(tools, llm);

        // Act
        var result = await selector.SelectToolAsync("Search for documents", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SelectToolAsync_LLMReturnsJsonWithExtraText_ExtractsJson()
    {
        // Arrange
        var tools = new List<ITool> { new MockTool("SearchTool", "Searches documents") };
        var llm = new MockChatCompletionModel((_, __) => Task.FromResult(
            @"Sure, I'll help with that. {""tool"": ""SearchTool"", ""arguments"": {""query"": ""test""}} Here you go!"));
        var selector = new Ouroboros.Agent.MetaAI.ToolSelector(tools, llm);

        // Act
        var result = await selector.SelectToolAsync("Search for test documents", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.ToolName.Should().Be("SearchTool");
        result.ArgumentsJson.Should().Contain("test");
    }

    [Fact]
    public async Task SelectToolAsync_LLMThrows_ReturnsNull()
    {
        // Arrange
        var tools = new List<ITool> { new MockTool("SearchTool", "Searches documents") };
        var llm = new MockChatCompletionModel((_, __) => throw new InvalidOperationException("LLM failure"));
        var selector = new Ouroboros.Agent.MetaAI.ToolSelector(tools, llm);

        // Act
        var result = await selector.SelectToolAsync("Search for documents", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SelectToolAsync_MultipleTools_LLMSelectsCorrectOne()
    {
        // Arrange
        var tools = new List<ITool>
        {
            new MockTool("SearchTool", "General search"),
            new MockTool("VectorSearchTool", "Vector-based semantic search"),
            new MockTool("DatabaseQueryTool", "SQL database queries")
        };
        var llm = new MockChatCompletionModel((_, __) => Task.FromResult(
            @"{""tool"": ""VectorSearchTool"", ""arguments"": {""query"": ""semantic search""}}"));
        var selector = new Ouroboros.Agent.MetaAI.ToolSelector(tools, llm);

        // Act
        var result = await selector.SelectToolAsync("Find semantically similar documents", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.ToolName.Should().Be("VectorSearchTool");
        result.ArgumentsJson.Should().Contain("semantic search");
    }

    [Fact]
    public async Task SelectToolAsync_ToolWithJsonSchema_IncludesSchemaInPrompt()
    {
        // Arrange
        string capturedPrompt = string.Empty;
        var tools = new List<ITool>
        {
            new MockTool("QueryTool", "Queries data", @"{""type"": ""object"", ""properties"": {""query"": {""type"": ""string""}}}")
        };
        var llm = new MockChatCompletionModel((prompt, ct) =>
        {
            capturedPrompt = prompt;
            return Task.FromResult(@"{""tool"": ""QueryTool"", ""arguments"": {""query"": ""test""}}");
        });
        var selector = new Ouroboros.Agent.MetaAI.ToolSelector(tools, llm);

        // Act
        await selector.SelectToolAsync("Query for test data", CancellationToken.None);

        // Assert
        capturedPrompt.Should().Contain("QueryTool");
        capturedPrompt.Should().Contain("Queries data");
        capturedPrompt.Should().Contain(@"{""type"": ""object""");
    }
}
