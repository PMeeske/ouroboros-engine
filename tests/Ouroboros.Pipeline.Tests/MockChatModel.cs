// <copyright file="MockChatModel.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Abstractions.Core;

namespace Ouroboros.Tests.Pipeline;

/// <summary>
/// Mock chat model for testing replay engine functionality.
/// </summary>
internal class MockChatModel : IChatCompletionModel
{
    private readonly string _response;

    public MockChatModel(string response)
    {
        _response = response;
    }

    public Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_response);
    }
}
