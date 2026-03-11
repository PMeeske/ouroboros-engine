// <copyright file="MockChatModel.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Abstractions.Core;

namespace Ouroboros.Tests.Mocks;

/// <summary>
/// Mock chat model for testing with call tracking and optional cancellation support.
/// </summary>
internal class MockChatModel : IChatCompletionModel
{
    private readonly string _response;
    private readonly bool _throwOnCancel;

    public int CallCount { get; private set; }
    public string? LastPrompt { get; private set; }

    public MockChatModel(string response, bool throwOnCancel = false)
    {
        _response = response;
        _throwOnCancel = throwOnCancel;
    }

    public Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        if (_throwOnCancel)
        {
            ct.ThrowIfCancellationRequested();
        }

        CallCount++;
        LastPrompt = prompt;
        return Task.FromResult(_response);
    }
}
