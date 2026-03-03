using FluentAssertions;
using LangChain.DocumentLoaders;
using LangChain.Providers;
using Ouroboros.LangChainBridge;
using Xunit;

namespace Ouroboros.LangChain.Tests;

[Trait("Category", "Unit")]
public class DocumentLoaderExtensionsTests
{
    private static Document CreateDocument(string content)
    {
        return new Document(content, new Dictionary<string, object>());
    }

    // --- ToContextMessage ---

    [Fact]
    public void ToContextMessage_NullDocuments_ThrowsArgumentNullException()
    {
        IEnumerable<Document> documents = null!;

        var act = () => documents.ToContextMessage();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ToContextMessage_EmptyDocuments_ReturnsEmptySystemMessage()
    {
        var documents = Enumerable.Empty<Document>();

        var result = documents.ToContextMessage();

        result.Content.Should().BeEmpty();
        result.Role.Should().Be(MessageRole.System);
    }

    [Fact]
    public void ToContextMessage_SingleDocument_ReturnsDocumentContent()
    {
        var documents = new[] { CreateDocument("Hello world") };

        var result = documents.ToContextMessage();

        result.Content.Should().Be("Hello world");
        result.Role.Should().Be(MessageRole.System);
    }

    [Fact]
    public void ToContextMessage_MultipleDocuments_JoinsWithDefaultSeparator()
    {
        var documents = new[]
        {
            CreateDocument("First"),
            CreateDocument("Second"),
            CreateDocument("Third"),
        };

        var result = documents.ToContextMessage();

        result.Content.Should().Be("First\n\n---\n\nSecond\n\n---\n\nThird");
    }

    [Fact]
    public void ToContextMessage_CustomSeparator_UsesCustomSeparator()
    {
        var documents = new[]
        {
            CreateDocument("A"),
            CreateDocument("B"),
        };

        var result = documents.ToContextMessage(" | ");

        result.Content.Should().Be("A | B");
    }

    [Fact]
    public void ToContextMessage_AlwaysReturnsSystemRole()
    {
        var documents = new[] { CreateDocument("text") };

        var result = documents.ToContextMessage();

        result.Role.Should().Be(MessageRole.System);
    }

    // --- ToRagRequest ---

    [Fact]
    public void ToRagRequest_NullDocuments_ThrowsArgumentNullException()
    {
        IEnumerable<Document> documents = null!;

        var act = () => documents.ToRagRequest("query");

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ToRagRequest_EmptyDocumentsWithQuery_ReturnsTwoMessages()
    {
        var documents = Enumerable.Empty<Document>();

        var result = documents.ToRagRequest("What is the answer?");

        result.Messages.Should().HaveCount(2);
    }

    [Fact]
    public void ToRagRequest_FirstMessageIsSystemWithDocumentContent()
    {
        var documents = new[]
        {
            CreateDocument("Context information here"),
        };

        var result = documents.ToRagRequest("query");

        result.Messages[0].Role.Should().Be(MessageRole.System);
        result.Messages[0].Content.Should().Be("Context information here");
    }

    [Fact]
    public void ToRagRequest_SecondMessageIsHumanWithQuery()
    {
        var documents = new[] { CreateDocument("context") };

        var result = documents.ToRagRequest("What is 2+2?");

        result.Messages[1].Role.Should().Be(MessageRole.Human);
        result.Messages[1].Content.Should().Be("What is 2+2?");
    }

    [Fact]
    public void ToRagRequest_MultipleDocuments_ConcatenatesWithDefaultSeparator()
    {
        var documents = new[]
        {
            CreateDocument("Doc1"),
            CreateDocument("Doc2"),
        };

        var result = documents.ToRagRequest("query");

        result.Messages[0].Content.Should().Be("Doc1\n\n---\n\nDoc2");
    }

    [Fact]
    public void ToRagRequest_CustomSeparator_UsesCustomSeparator()
    {
        var documents = new[]
        {
            CreateDocument("A"),
            CreateDocument("B"),
        };

        var result = documents.ToRagRequest("query", " ## ");

        result.Messages[0].Content.Should().Be("A ## B");
    }
}
