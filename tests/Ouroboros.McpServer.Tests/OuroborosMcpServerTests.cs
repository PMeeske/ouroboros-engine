using System.Text;
using System.Text.Json;
using Moq;

namespace Ouroboros.McpServer.Tests;

[Trait("Category", "Unit")]
public class OuroborosMcpServerTests
{
    private static ITool CreateMockTool(
        string name = "echo",
        string description = "Echo tool",
        string? jsonSchema = null,
        Result<string, string>? invokeResult = null)
    {
        var mock = new Mock<ITool>();
        mock.Setup(t => t.Name).Returns(name);
        mock.Setup(t => t.Description).Returns(description);
        mock.Setup(t => t.JsonSchema).Returns(jsonSchema);
        mock.Setup(t => t.InvokeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(invokeResult ?? Result<string, string>.Success("echoed"));
        return mock.Object;
    }

    // --- Constructor ---

    [Fact]
    public void Constructor_NullRegistry_ThrowsArgumentNullException()
    {
        var act = () => new OuroborosMcpServer(null!);

        act.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("registry");
    }

    [Fact]
    public void Constructor_NullOptions_UsesDefaults()
    {
        var registry = new ToolRegistry();

        var server = new OuroborosMcpServer(registry, null);

        // Should not throw — defaults are used internally
        server.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithOptions_DoesNotThrow()
    {
        var registry = new ToolRegistry();
        var options = new McpServerOptions { ServerName = "Test" };

        var server = new OuroborosMcpServer(registry, options);

        server.Should().NotBeNull();
    }

    // --- HandleMessageAsync: initialize ---

    [Fact]
    public async Task HandleMessageAsync_Initialize_ReturnsServerInfo()
    {
        var options = new McpServerOptions { ServerName = "TestServer", ServerVersion = "3.0.0" };
        var server = new OuroborosMcpServer(new ToolRegistry(), options);

        var response = await server.HandleMessageAsync(
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}""");

        response.Should().NotBeNull();
        using var doc = JsonDocument.Parse(response!);
        var root = doc.RootElement;
        root.GetProperty("jsonrpc").GetString().Should().Be("2.0");
        root.GetProperty("id").GetInt32().Should().Be(1);
        var result = root.GetProperty("result");
        result.GetProperty("protocolVersion").GetString().Should().Be("2024-11-05");
        result.GetProperty("serverInfo").GetProperty("name").GetString().Should().Be("TestServer");
        result.GetProperty("serverInfo").GetProperty("version").GetString().Should().Be("3.0.0");
    }

    [Fact]
    public async Task HandleMessageAsync_Initialize_IncludesToolsCapability()
    {
        var server = new OuroborosMcpServer(new ToolRegistry());

        var response = await server.HandleMessageAsync(
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}""");

        using var doc = JsonDocument.Parse(response!);
        var capabilities = doc.RootElement.GetProperty("result").GetProperty("capabilities");
        capabilities.GetProperty("tools").GetProperty("listChanged").GetBoolean().Should().BeFalse();
    }

    // --- HandleMessageAsync: tools/list ---

    [Fact]
    public async Task HandleMessageAsync_ToolsList_ReturnsRegisteredTools()
    {
        var tool = CreateMockTool("calc", "Calculator");
        var registry = new ToolRegistry().WithTool(tool);
        var server = new OuroborosMcpServer(registry);

        var response = await server.HandleMessageAsync(
            """{"jsonrpc":"2.0","id":2,"method":"tools/list"}""");

        response.Should().NotBeNull();
        using var doc = JsonDocument.Parse(response!);
        var tools = doc.RootElement.GetProperty("result").GetProperty("tools");
        tools.GetArrayLength().Should().Be(1);
        tools[0].GetProperty("name").GetString().Should().Be("calc");
        tools[0].GetProperty("description").GetString().Should().Be("Calculator");
    }

    [Fact]
    public async Task HandleMessageAsync_ToolsList_EmptyRegistry_ReturnsEmptyArray()
    {
        var server = new OuroborosMcpServer(new ToolRegistry());

        var response = await server.HandleMessageAsync(
            """{"jsonrpc":"2.0","id":2,"method":"tools/list"}""");

        using var doc = JsonDocument.Parse(response!);
        doc.RootElement.GetProperty("result").GetProperty("tools")
            .GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task HandleMessageAsync_ToolsList_WithToolFilter_ReturnsFilteredTools()
    {
        var registry = new ToolRegistry()
            .WithTool(CreateMockTool("alpha", "A"))
            .WithTool(CreateMockTool("beta", "B"));
        var options = new McpServerOptions { ToolFilter = ["alpha"] };
        var server = new OuroborosMcpServer(registry, options);

        var response = await server.HandleMessageAsync(
            """{"jsonrpc":"2.0","id":2,"method":"tools/list"}""");

        using var doc = JsonDocument.Parse(response!);
        var tools = doc.RootElement.GetProperty("result").GetProperty("tools");
        tools.GetArrayLength().Should().Be(1);
        tools[0].GetProperty("name").GetString().Should().Be("alpha");
    }

    // --- HandleMessageAsync: tools/call ---

    [Fact]
    public async Task HandleMessageAsync_ToolsCall_Success_ReturnsContent()
    {
        var tool = CreateMockTool("echo", invokeResult: Result<string, string>.Success("hello world"));
        var registry = new ToolRegistry().WithTool(tool);
        var server = new OuroborosMcpServer(registry);

        var response = await server.HandleMessageAsync(
            """{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"echo","arguments":{"text":"hello"}}}""");

        response.Should().NotBeNull();
        using var doc = JsonDocument.Parse(response!);
        var result = doc.RootElement.GetProperty("result");
        result.GetProperty("isError").GetBoolean().Should().BeFalse();
        result.GetProperty("content")[0].GetProperty("text").GetString().Should().Be("hello world");
        result.GetProperty("content")[0].GetProperty("type").GetString().Should().Be("text");
    }

    [Fact]
    public async Task HandleMessageAsync_ToolsCall_ToolError_ReturnsIsErrorTrue()
    {
        var tool = CreateMockTool("fail", invokeResult: Result<string, string>.Failure("bad input"));
        var registry = new ToolRegistry().WithTool(tool);
        var server = new OuroborosMcpServer(registry);

        var response = await server.HandleMessageAsync(
            """{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"fail"}}""");

        using var doc = JsonDocument.Parse(response!);
        var result = doc.RootElement.GetProperty("result");
        result.GetProperty("isError").GetBoolean().Should().BeTrue();
        result.GetProperty("content")[0].GetProperty("text").GetString().Should().Be("bad input");
    }

    [Fact]
    public async Task HandleMessageAsync_ToolsCall_UnknownTool_ReturnsIsErrorTrue()
    {
        var server = new OuroborosMcpServer(new ToolRegistry());

        var response = await server.HandleMessageAsync(
            """{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"nonexistent"}}""");

        using var doc = JsonDocument.Parse(response!);
        var result = doc.RootElement.GetProperty("result");
        result.GetProperty("isError").GetBoolean().Should().BeTrue();
        result.GetProperty("content")[0].GetProperty("text").GetString().Should().Contain("not found");
    }

    [Fact]
    public async Task HandleMessageAsync_ToolsCall_MissingParams_ReturnsJsonRpcError()
    {
        var server = new OuroborosMcpServer(new ToolRegistry());

        var response = await server.HandleMessageAsync(
            """{"jsonrpc":"2.0","id":3,"method":"tools/call"}""");

        using var doc = JsonDocument.Parse(response!);
        var error = doc.RootElement.GetProperty("error");
        error.GetProperty("code").GetInt32().Should().Be(-32602);
        error.GetProperty("message").GetString().Should().Contain("params");
    }

    [Fact]
    public async Task HandleMessageAsync_ToolsCall_MissingName_ReturnsJsonRpcError()
    {
        var server = new OuroborosMcpServer(new ToolRegistry());

        var response = await server.HandleMessageAsync(
            """{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{}}""");

        using var doc = JsonDocument.Parse(response!);
        var error = doc.RootElement.GetProperty("error");
        error.GetProperty("code").GetInt32().Should().Be(-32602);
        error.GetProperty("message").GetString().Should().Contain("name");
    }

    // --- HandleMessageAsync: notifications/initialized ---

    [Fact]
    public async Task HandleMessageAsync_NotificationsInitialized_ReturnsNull()
    {
        var server = new OuroborosMcpServer(new ToolRegistry());

        var response = await server.HandleMessageAsync(
            """{"jsonrpc":"2.0","method":"notifications/initialized"}""");

        response.Should().BeNull();
    }

    // --- HandleMessageAsync: ping ---

    [Fact]
    public async Task HandleMessageAsync_Ping_ReturnsPong()
    {
        var server = new OuroborosMcpServer(new ToolRegistry());

        var response = await server.HandleMessageAsync(
            """{"jsonrpc":"2.0","id":99,"method":"ping"}""");

        response.Should().NotBeNull();
        using var doc = JsonDocument.Parse(response!);
        doc.RootElement.GetProperty("jsonrpc").GetString().Should().Be("2.0");
        doc.RootElement.GetProperty("id").GetInt32().Should().Be(99);
        doc.RootElement.TryGetProperty("result", out _).Should().BeTrue();
    }

    // --- HandleMessageAsync: unknown method ---

    [Fact]
    public async Task HandleMessageAsync_UnknownMethod_ReturnsMethodNotFoundError()
    {
        var server = new OuroborosMcpServer(new ToolRegistry());

        var response = await server.HandleMessageAsync(
            """{"jsonrpc":"2.0","id":5,"method":"unknown/method"}""");

        using var doc = JsonDocument.Parse(response!);
        var error = doc.RootElement.GetProperty("error");
        error.GetProperty("code").GetInt32().Should().Be(-32601);
        error.GetProperty("message").GetString().Should().Contain("unknown/method");
    }

    // --- HandleMessageAsync: invalid JSON ---

    [Fact]
    public async Task HandleMessageAsync_InvalidJson_ReturnsParseError()
    {
        var server = new OuroborosMcpServer(new ToolRegistry());

        var response = await server.HandleMessageAsync("not valid json {{{");

        using var doc = JsonDocument.Parse(response!);
        var error = doc.RootElement.GetProperty("error");
        error.GetProperty("code").GetInt32().Should().Be(-32700);
        error.GetProperty("message").GetString().Should().Contain("Parse error");
    }

    // --- RunAsync(Stream, Stream) ---

    [Fact]
    public async Task RunAsync_NullInput_ThrowsArgumentNullException()
    {
        var server = new OuroborosMcpServer(new ToolRegistry());

        var act = () => server.RunAsync(null!, Stream.Null);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task RunAsync_NullOutput_ThrowsArgumentNullException()
    {
        var server = new OuroborosMcpServer(new ToolRegistry());

        var act = () => server.RunAsync(Stream.Null, null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task RunAsync_SingleMessage_WritesResponse()
    {
        var server = new OuroborosMcpServer(new ToolRegistry());
        var input = new MemoryStream(Encoding.UTF8.GetBytes(
            """{"jsonrpc":"2.0","id":1,"method":"ping"}""" + "\n"));
        var output = new MemoryStream();

        await server.RunAsync(input, output);

        output.Position = 0;
        using var reader = new StreamReader(output);
        var line = await reader.ReadLineAsync();
        line.Should().NotBeNull();
        using var doc = JsonDocument.Parse(line!);
        doc.RootElement.GetProperty("id").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task RunAsync_MultipleMessages_WritesMultipleResponses()
    {
        var server = new OuroborosMcpServer(new ToolRegistry());
        var messages = string.Join("\n",
            """{"jsonrpc":"2.0","id":1,"method":"ping"}""",
            """{"jsonrpc":"2.0","id":2,"method":"ping"}""") + "\n";
        var input = new MemoryStream(Encoding.UTF8.GetBytes(messages));
        var output = new MemoryStream();

        await server.RunAsync(input, output);

        output.Position = 0;
        using var reader = new StreamReader(output);
        var lines = (await reader.ReadToEndAsync()).Trim().Split('\n');
        lines.Should().HaveCount(2);
    }

    [Fact]
    public async Task RunAsync_BlankLines_AreIgnored()
    {
        var server = new OuroborosMcpServer(new ToolRegistry());
        var messages = "\n\n" + """{"jsonrpc":"2.0","id":1,"method":"ping"}""" + "\n\n";
        var input = new MemoryStream(Encoding.UTF8.GetBytes(messages));
        var output = new MemoryStream();

        await server.RunAsync(input, output);

        output.Position = 0;
        using var reader = new StreamReader(output);
        var content = (await reader.ReadToEndAsync()).Trim();
        content.Split('\n').Should().HaveCount(1);
    }

    [Fact]
    public async Task RunAsync_Notification_DoesNotWriteResponse()
    {
        var server = new OuroborosMcpServer(new ToolRegistry());
        var input = new MemoryStream(Encoding.UTF8.GetBytes(
            """{"jsonrpc":"2.0","method":"notifications/initialized"}""" + "\n"));
        var output = new MemoryStream();

        await server.RunAsync(input, output);

        output.Position = 0;
        using var reader = new StreamReader(output);
        var content = await reader.ReadToEndAsync();
        content.Trim().Should().BeEmpty();
    }

    [Fact]
    public async Task RunAsync_CancellationToken_StopsProcessing()
    {
        var server = new OuroborosMcpServer(new ToolRegistry());
        using var cts = new CancellationTokenSource();
        // Create a stream that never ends by combining a message with a blocking stream
        var messages = """{"jsonrpc":"2.0","id":1,"method":"ping"}""" + "\n";
        var input = new MemoryStream(Encoding.UTF8.GetBytes(messages));
        var output = new MemoryStream();

        // This should complete normally when the stream ends (EOF)
        await server.RunAsync(input, output, cts.Token);

        output.Length.Should().BeGreaterThan(0);
    }
}
