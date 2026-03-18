using LangChain.DocumentLoaders;
using System.Reactive.Linq;

namespace Ouroboros.Pipeline.Reasoning;

public static partial class ReasoningArrows
{
    private static readonly System.Text.RegularExpressions.Regex StreamingToolPattern =
        new(@"\[TOOL:([^\s]+)\s*([^\]]*)\]", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// Creates a streaming thinking arrow that emits reasoning chunks in real-time.
    /// </summary>
    public static IObservable<(string chunk, PipelineBranch branch)> StreamingThinkingArrow(
        Ouroboros.Providers.IStreamingChatModel streamingModel,
        ToolRegistry tools,
        IEmbeddingModel embed,
        string topic,
        string query,
        int k = 8)
    {
        async IAsyncEnumerable<(string chunk, PipelineBranch branch)> StreamAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            PipelineBranch branch = new PipelineBranch("streaming", new TrackedVectorStore(), DataSource.FromPath("."));
            IReadOnlyCollection<Document> docs = await branch.Store.GetSimilarDocuments(embed, query, amount: k).ConfigureAwait(false);
            string context = string.Join("\n---\n", docs.Select(d => d.PageContent));

            string prompt = Prompts.Thinking.Format(new()
            {
                ["context"] = context,
                ["topic"] = topic,
                ["tools_schemas"] = ToolSchemasOrEmpty(tools)
            });

            System.Text.StringBuilder fullText = new System.Text.StringBuilder();
            System.Text.StringBuilder currentTurnText = new System.Text.StringBuilder();
            List<ToolExecution> allToolCalls = new List<ToolExecution>();

            while (!ct.IsCancellationRequested)
            {
                bool toolCalled = false;
                currentTurnText.Clear();

                string? lastFullText = null;
                await foreach (string chunk in streamingModel.StreamReasoningContent(prompt, ct).ToAsyncEnumerable().ConfigureAwait(false))
                {
                    fullText.Append(chunk);
                    currentTurnText.Append(chunk);

                    lastFullText = fullText.ToString();
                    PipelineBranch updatedBranch = branch.WithReasoning(new Thinking(lastFullText), prompt, allToolCalls);
                    yield return (chunk, updatedBranch);
                }

                var matches = StreamingToolPattern.Matches(currentTurnText.ToString());

                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    toolCalled = true;
                    string name = match.Groups[1].Value;
                    string args = match.Groups[2].Value.Trim();

                    if (string.IsNullOrWhiteSpace(name))
                    {
                        string errorResult = "error: empty tool name";
                        ToolExecution errorExecution = new ToolExecution("?", args, errorResult, DateTime.UtcNow);
                        allToolCalls.Add(errorExecution);
                        string errorTag = $"[TOOL-RESULT:?] {errorResult}";
                        fullText.Append(errorTag);
                        PipelineBranch withError = branch.WithReasoning(new Thinking(fullText.ToString()), prompt, allToolCalls);
                        yield return (errorTag, withError);

                        continue;
                    }

                    ITool? tool = tools.Get(name);
                    string output;
                    if (tool is null)
                    {
                        output = $"error: tool '{name}' not found";
                    }
                    else
                    {
                        try
                        {
                            Result<string, string> toolResult = await tool.InvokeAsync(args, ct).ConfigureAwait(false);
                            output = toolResult.Match(success => success, error => $"error: {error}");
                        }
                        catch (OperationCanceledException) { throw; }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            output = $"error: {ex.Message}";
                        }
                    }

                    ToolExecution execution = new ToolExecution(name, args, output, DateTime.UtcNow);
                    allToolCalls.Add(execution);

                    string resultTag = $"[TOOL-RESULT:{name}] {output}";

                    fullText.Append(resultTag);
                    PipelineBranch withTool = branch.WithReasoning(new Thinking(fullText.ToString()), prompt, allToolCalls);
                    yield return (resultTag, withTool);

                    prompt += $"\n{match.Value} {resultTag}\nContinue thinking.";
                }

                if (!toolCalled)
                {
                    break;
                }
            }
        }

        return StreamAsync().ToObservable();
    }

    /// <summary>
    /// Creates a streaming draft arrow using Reactive Extensions.
    /// </summary>
    public static IObservable<(string chunk, PipelineBranch branch)> StreamingDraftArrow(
        Ouroboros.Providers.IStreamingChatModel streamingModel,
        ToolRegistry tools,
        IEmbeddingModel embed,
        PipelineBranch inputBranch,
        string topic,
        string query,
        int k = 8)
    {
        async IAsyncEnumerable<(string chunk, PipelineBranch branch)> StreamAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            PipelineBranch branch = inputBranch;
            IReadOnlyCollection<Document> docs = await branch.Store.GetSimilarDocuments(embed, query, amount: k).ConfigureAwait(false);
            string context = string.Join("\n---\n", docs.Select(d => d.PageContent));

            string prompt = Prompts.Draft.Format(new()
            {
                ["context"] = context,
                ["topic"] = topic,
                ["tools_schemas"] = ToolSchemasOrEmpty(tools)
            });

            System.Text.StringBuilder fullText = new System.Text.StringBuilder();

            await foreach (string chunk in streamingModel.StreamReasoningContent(prompt, ct).ToAsyncEnumerable().ConfigureAwait(false))
            {
                fullText.Append(chunk);
                PipelineBranch updatedBranch = branch.WithReasoning(new Draft(fullText.ToString()), prompt, null);
                yield return (chunk, updatedBranch);
            }
        }

        return StreamAsync().ToObservable();
    }

    /// <summary>
    /// Creates a streaming critique arrow.
    /// </summary>
    public static Result<IObservable<(string chunk, PipelineBranch branch)>, string> StreamingCritiqueArrow(
        Ouroboros.Providers.IStreamingChatModel streamingModel,
        ToolRegistry tools,
        IEmbeddingModel embed,
        PipelineBranch inputBranch,
        string topic,
        string query,
        int k = 8)
    {
        ReasoningState? currentState = GetMostRecentReasoningState(inputBranch);
        if (currentState is null)
            return Result<IObservable<(string chunk, PipelineBranch branch)>, string>.Failure("No draft or previous improvement found to critique");

        async IAsyncEnumerable<(string chunk, PipelineBranch branch)> StreamAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {

            IReadOnlyCollection<Document> docs = await inputBranch.Store.GetSimilarDocuments(embed, query, amount: k).ConfigureAwait(false);
            string context = string.Join("\n---\n", docs.Select(d => d.PageContent));

            string prompt = Prompts.Critique.Format(new()
            {
                ["context"] = context,
                ["draft"] = currentState.Text,
                ["topic"] = topic,
                ["tools_schemas"] = ToolSchemasOrEmpty(tools)
            });

            System.Text.StringBuilder fullText = new System.Text.StringBuilder();

            await foreach (string chunk in streamingModel.StreamReasoningContent(prompt, ct).ToAsyncEnumerable().ConfigureAwait(false))
            {
                fullText.Append(chunk);
                PipelineBranch updatedBranch = inputBranch.WithReasoning(new Critique(fullText.ToString()), prompt, null);
                yield return (chunk, updatedBranch);
            }
        }

        return Result<IObservable<(string chunk, PipelineBranch branch)>, string>.Success(StreamAsync().ToObservable());
    }

    /// <summary>
    /// Creates a streaming improvement arrow.
    /// </summary>
    public static Result<IObservable<(string chunk, PipelineBranch branch)>, string> StreamingImproveArrow(
        Ouroboros.Providers.IStreamingChatModel streamingModel,
        ToolRegistry tools,
        IEmbeddingModel embed,
        PipelineBranch inputBranch,
        string topic,
        string query,
        int k = 8)
    {
        ReasoningState? currentState = GetMostRecentReasoningState(inputBranch);
        Critique? critique = inputBranch.Events.OfType<ReasoningStep>().Select(e => e.State).OfType<Critique>().LastOrDefault();

        if (currentState is null)
            return Result<IObservable<(string chunk, PipelineBranch branch)>, string>.Failure("No draft or previous improvement found");
        if (critique is null)
            return Result<IObservable<(string chunk, PipelineBranch branch)>, string>.Failure("No critique found for improvement");

        async IAsyncEnumerable<(string chunk, PipelineBranch branch)> StreamAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {

            IReadOnlyCollection<Document> docs = await inputBranch.Store.GetSimilarDocuments(embed, query, amount: k).ConfigureAwait(false);
            string context = string.Join("\n---\n", docs.Select(d => d.PageContent));

            string prompt = Prompts.Improve.Format(new()
            {
                ["context"] = context,
                ["draft"] = currentState.Text,
                ["critique"] = critique.CritiqueText,
                ["topic"] = topic,
                ["tools_schemas"] = ToolSchemasOrEmpty(tools)
            });

            System.Text.StringBuilder fullText = new System.Text.StringBuilder();

            await foreach (string chunk in streamingModel.StreamReasoningContent(prompt, ct).ToAsyncEnumerable().ConfigureAwait(false))
            {
                fullText.Append(chunk);
                PipelineBranch updatedBranch = inputBranch.WithReasoning(new FinalSpec(fullText.ToString()), prompt, null);
                yield return (chunk, updatedBranch);
            }
        }

        return Result<IObservable<(string chunk, PipelineBranch branch)>, string>.Success(StreamAsync().ToObservable());
    }

    /// <summary>
    /// Creates a complete streaming reasoning pipeline.
    /// </summary>
    public static IObservable<(string stage, string chunk, PipelineBranch branch)> StreamingReasoningPipeline(
        Ouroboros.Providers.IStreamingChatModel streamingModel,
        ToolRegistry tools,
        IEmbeddingModel embed,
        string topic,
        string query,
        int k = 8)
    {
        return Observable.Create<(string stage, string chunk, PipelineBranch branch)>(async (observer, ct) =>
        {
            try
            {
                PipelineBranch branch = new PipelineBranch("streaming-pipeline", new TrackedVectorStore(), DataSource.FromPath("."));

                await StreamingThinkingArrow(streamingModel, tools, embed, topic, query, k)
                    .Do(tuple => observer.OnNext(("Thinking", tuple.chunk, tuple.branch)))
                    .LastAsync()
                    .ForEachAsync(tuple => branch = tuple.branch, ct).ConfigureAwait(false);

                await StreamingDraftArrow(streamingModel, tools, embed, branch, topic, query, k)
                    .Do(tuple => observer.OnNext(("Draft", tuple.chunk, tuple.branch)))
                    .LastAsync()
                    .ForEachAsync(tuple => branch = tuple.branch, ct).ConfigureAwait(false);

                var critiqueResult = StreamingCritiqueArrow(streamingModel, tools, embed, branch, topic, query, k);
                if (!critiqueResult.IsSuccess)
                {
                    observer.OnError(new InvalidOperationException(critiqueResult.Error));
                    return;
                }

                await critiqueResult.Value
                    .Do(tuple => observer.OnNext(("Critique", tuple.chunk, tuple.branch)))
                    .LastAsync()
                    .ForEachAsync(tuple => branch = tuple.branch, ct).ConfigureAwait(false);

                var improveResult = StreamingImproveArrow(streamingModel, tools, embed, branch, topic, query, k);
                if (!improveResult.IsSuccess)
                {
                    observer.OnError(new InvalidOperationException(improveResult.Error));
                    return;
                }

                await improveResult.Value
                    .Do(tuple => observer.OnNext(("Improve", tuple.chunk, tuple.branch)))
                    .LastAsync()
                    .ForEachAsync(tuple => branch = tuple.branch, ct).ConfigureAwait(false);

                observer.OnCompleted();
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                observer.OnError(ex);
            }
        });
    }

    // -------------------------------------------------------
    //  ReactiveKleisli-typed streaming arrows
    //  Proper A -> IObservable<B> category for composition.
    // -------------------------------------------------------

    /// <summary>
    /// Kleisli-typed streaming draft arrow.
    /// <c>ReactiveKleisli&lt;PipelineBranch, (string, PipelineBranch)&gt;</c>
    /// composes with .Then(), .Map(), .Where() etc. via ReactiveKleisliExtensions.
    /// </summary>
    public static ReactiveKleisli<PipelineBranch, (string chunk, PipelineBranch branch)> StreamingDraftKleisli(
        Ouroboros.Providers.IStreamingChatModel streamingModel,
        ToolRegistry tools,
        IEmbeddingModel embed,
        string topic,
        string query,
        int k = 8)
        => branch => StreamingDraftArrow(streamingModel, tools, embed, branch, topic, query, k);

    /// <summary>
    /// Kleisli-typed streaming critique arrow.
    /// Returns an observable that errors on invalid state rather than using Result.
    /// </summary>
    public static ReactiveKleisli<PipelineBranch, (string chunk, PipelineBranch branch)> StreamingCritiqueKleisli(
        Ouroboros.Providers.IStreamingChatModel streamingModel,
        ToolRegistry tools,
        IEmbeddingModel embed,
        string topic,
        string query,
        int k = 8)
        => branch =>
        {
            var result = StreamingCritiqueArrow(streamingModel, tools, embed, branch, topic, query, k);
            return result.IsSuccess
                ? result.Value
                : Observable.Throw<(string chunk, PipelineBranch branch)>(
                    new InvalidOperationException(result.Error));
        };

    /// <summary>
    /// Kleisli-typed streaming improvement arrow.
    /// Returns an observable that errors on invalid state rather than using Result.
    /// </summary>
    public static ReactiveKleisli<PipelineBranch, (string chunk, PipelineBranch branch)> StreamingImproveKleisli(
        Ouroboros.Providers.IStreamingChatModel streamingModel,
        ToolRegistry tools,
        IEmbeddingModel embed,
        string topic,
        string query,
        int k = 8)
        => branch =>
        {
            var result = StreamingImproveArrow(streamingModel, tools, embed, branch, topic, query, k);
            return result.IsSuccess
                ? result.Value
                : Observable.Throw<(string chunk, PipelineBranch branch)>(
                    new InvalidOperationException(result.Error));
        };
}
