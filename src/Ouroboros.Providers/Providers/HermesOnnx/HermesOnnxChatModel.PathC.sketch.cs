// PATH C SKETCH — gated, not compiled.
#if HERMES_ONNX_PATH_C

using Microsoft.ML.Tokenizers;
using Microsoft.ML.OnnxRuntimeGenAI;

namespace Ouroboros.Providers.HermesOnnx;

internal sealed class HermesOnnxChatModelPathC : IDisposable
{
    private readonly Model _model;
    private readonly BpeTokenizer _tokenizer;
    private readonly int _eosTokenId;

    public HermesOnnxChatModelPathC(string modelPath)
    {
        _model = new Model(modelPath);

        using FileStream s = File.OpenRead(Path.Combine(modelPath, "tokenizer.json"));
        _tokenizer = BpeTokenizer.Create(s);

        _eosTokenId = _tokenizer.EncodeToIds("<|eot_id|>")[0];
    }

    public string Generate(string prompt, int maxTokens = 512)
    {
        int[] inputIds = _tokenizer.EncodeToIds(prompt).ToArray();

        using GeneratorParams gp = new(_model);
        gp.SetSearchOption("max_length", maxTokens);
        using Generator g = new(_model, gp);
        g.AppendTokenSequences(new[] { inputIds });

        List<int> outputIds = new();
        while (!g.IsDone())
        {
            g.GenerateNextToken();
            int next = g.GetSequence(0)[^1];
            if (next == _eosTokenId) break;
            outputIds.Add(next);
        }

        return _tokenizer.Decode(outputIds);
    }

    public void Dispose() => _model.Dispose();
}

#endif
