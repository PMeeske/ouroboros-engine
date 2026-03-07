// <copyright file="OuroborosAutoFunctionFilterTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Ouroboros.SemanticKernel.Filters;

namespace Ouroboros.SemanticKernel.Tests.Filters;

public sealed class OuroborosAutoFunctionFilterTests
{
    private readonly Mock<ILogger<OuroborosAutoFunctionFilter>> _mockLogger = new();

    private OuroborosAutoFunctionFilter CreateFilter() => new(_mockLogger.Object);

    // ── Constructor ──────────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        var act = () => new OuroborosAutoFunctionFilter(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void Constructor_ValidLogger_DoesNotThrow()
    {
        var act = () => CreateFilter();
        act.Should().NotThrow();
    }

    // ── AfterInvoke property ─────────────────────────────────────────────

    [Fact]
    public void AfterInvoke_DefaultIsNull()
    {
        var filter = CreateFilter();
        filter.AfterInvoke.Should().BeNull();
    }

    [Fact]
    public void AfterInvoke_CanBeSetAndRetrieved()
    {
        var filter = CreateFilter();
        Action<string, string, TimeSpan, bool> callback = (_, _, _, _) => { };

        filter.AfterInvoke = callback;

        filter.AfterInvoke.Should().BeSameAs(callback);
    }

    // ── OnAutoFunctionInvocationAsync ────────────────────────────────────

    [Fact]
    public async Task OnAutoFunctionInvocationAsync_NullContext_ThrowsArgumentNullException()
    {
        var filter = CreateFilter();
        Func<AutoFunctionInvocationContext, Task> next = _ => Task.CompletedTask;

        var act = () => filter.OnAutoFunctionInvocationAsync(null!, next);

        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("context");
    }

    [Fact]
    public async Task OnAutoFunctionInvocationAsync_NullNext_ThrowsArgumentNullException()
    {
        var filter = CreateFilter();
        var context = CreateMockContext();

        var act = () => filter.OnAutoFunctionInvocationAsync(context, null!);

        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("next");
    }

    [Fact]
    public async Task OnAutoFunctionInvocationAsync_Success_CallsNextAndLogs()
    {
        var filter = CreateFilter();
        var context = CreateMockContext();
        bool nextCalled = false;

        await filter.OnAutoFunctionInvocationAsync(context, _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task OnAutoFunctionInvocationAsync_Success_InvokesAfterInvokeCallback()
    {
        var filter = CreateFilter();
        var context = CreateMockContext();

        string? capturedPlugin = null;
        string? capturedFunction = null;
        bool? capturedSuccess = null;

        filter.AfterInvoke = (plugin, function, _, succeeded) =>
        {
            capturedPlugin = plugin;
            capturedFunction = function;
            capturedSuccess = succeeded;
        };

        await filter.OnAutoFunctionInvocationAsync(context, _ => Task.CompletedTask);

        capturedPlugin.Should().NotBeNull();
        capturedFunction.Should().Be("TestFunction");
        capturedSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task OnAutoFunctionInvocationAsync_NextThrows_PropagatesException()
    {
        var filter = CreateFilter();
        var context = CreateMockContext();
        var expectedException = new InvalidOperationException("Test error");

        var act = () => filter.OnAutoFunctionInvocationAsync(context, _ => throw expectedException);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Test error");
    }

    [Fact]
    public async Task OnAutoFunctionInvocationAsync_NextThrows_InvokesAfterInvokeWithFalse()
    {
        var filter = CreateFilter();
        var context = CreateMockContext();
        bool? capturedSuccess = null;

        filter.AfterInvoke = (_, _, _, succeeded) => capturedSuccess = succeeded;

        try
        {
            await filter.OnAutoFunctionInvocationAsync(
                context,
                _ => throw new InvalidOperationException("boom"));
        }
        catch (InvalidOperationException)
        {
            // Expected
        }

        capturedSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task OnAutoFunctionInvocationAsync_OperationCancelled_RethrowsDirectly()
    {
        var filter = CreateFilter();
        var context = CreateMockContext();

        var act = () => filter.OnAutoFunctionInvocationAsync(
            context,
            _ => throw new OperationCanceledException());

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task OnAutoFunctionInvocationAsync_AfterInvokeElapsed_IsPositive()
    {
        var filter = CreateFilter();
        var context = CreateMockContext();
        TimeSpan? capturedElapsed = null;

        filter.AfterInvoke = (_, _, elapsed, _) => capturedElapsed = elapsed;

        await filter.OnAutoFunctionInvocationAsync(context, async _ =>
        {
            await Task.Delay(10);
        });

        capturedElapsed.Should().NotBeNull();
        capturedElapsed!.Value.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    [Fact]
    public async Task OnAutoFunctionInvocationAsync_NullPluginName_DefaultsToNone()
    {
        var filter = CreateFilter();
        var context = CreateMockContext(pluginName: null);
        string? capturedPlugin = null;

        filter.AfterInvoke = (plugin, _, _, _) => capturedPlugin = plugin;

        await filter.OnAutoFunctionInvocationAsync(context, _ => Task.CompletedTask);

        capturedPlugin.Should().Be("(none)");
    }

    // ── Helper methods ───────────────────────────────────────────────────

    private static AutoFunctionInvocationContext CreateMockContext(string? pluginName = "TestPlugin")
    {
        var function = KernelFunctionFactory.CreateFromMethod(
            () => "result",
            functionName: "TestFunction",
            description: "A test function");

        // KernelPlugin is needed to associate pluginName with the function
        KernelFunction functionWithPlugin;
        if (pluginName is not null)
        {
            var plugin = KernelPluginFactory.CreateFromFunctions(pluginName, new[] { function });
            functionWithPlugin = plugin.First();
        }
        else
        {
            functionWithPlugin = function;
        }

        var kernel = Kernel.CreateBuilder().Build();
        var chatHistory = new Microsoft.SemanticKernel.ChatCompletion.ChatHistory();
        var chatMessage = new Microsoft.SemanticKernel.ChatMessageContent(
            Microsoft.SemanticKernel.ChatCompletion.AuthorRole.Assistant, "test");

        return new AutoFunctionInvocationContext(kernel, functionWithPlugin, new FunctionResult(functionWithPlugin), chatHistory, chatMessage);
    }
}
