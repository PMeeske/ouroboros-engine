using OllamaSharp.Models;

namespace Ouroboros.Providers;

public static class OllamaPresets
{
    /// <summary>
    /// Gets preset for DeepSeek Coder 33B with conservative defaults optimized for code generation.
    /// Automatically adapts threads, context window, and GPU usage based on <see cref="MachineCapabilities"/>.
    /// </summary>
    public static RequestOptions DeepSeekCoder33B
    {
        get
        {
            int cores = MachineCapabilities.CpuCores;
            long memMb = MachineCapabilities.TotalMemoryMb;
            int gpus = MachineCapabilities.GpuCount;

            // conservative defaults
            return new RequestOptions
            {
                NumCtx = memMb > 64000 ? 8192 : 4096, // more RAM -> larger context
                NumThread = Math.Max(1, cores - 1),
                NumGpu = gpus > 0 ? gpus : 0,
                MainGpu = 0,
                LowVram = gpus == 0, // force low-VRAM path if CPU-only
                Temperature = 0.2f,  // coder -> low creativity
                TopP = 0.9f,
                TopK = 40,
                RepeatPenalty = 1.1f,
                UseMmap = true,
                UseMlock = false,
            };
        }
    }

    /// <summary>
    /// KeepAlive duration in seconds for DeepSeek Coder 33B.
    /// Apply via <see cref="GenerateRequest.KeepAlive"/> since it is not part of <see cref="RequestOptions"/>.
    /// </summary>
    public const string DeepSeekCoder33BKeepAlive = "10m";

    /// <summary>
    /// Gets preset for Llama 3 (general conversation). Balanced temperature and retrieval-friendly settings.
    /// Adapts to available CPU cores, memory size and GPU count.
    /// </summary>
    public static RequestOptions Llama3General
    {
        get
        {
            int cores = MachineCapabilities.CpuCores;
            long memMb = MachineCapabilities.TotalMemoryMb;
            int gpus = MachineCapabilities.GpuCount;

            return new RequestOptions
            {
                NumCtx = memMb > 32000 ? 8192 : 4096,
                NumThread = Math.Max(1, cores - 1),
                NumGpu = gpus > 0 ? Math.Min(gpus, 1) : 0,
                MainGpu = 0,
                LowVram = gpus == 0,
                Temperature = 0.5f, // balanced for general chat
                TopP = 0.9f,
                TopK = 40,
                RepeatPenalty = 1.1f,
                UseMmap = true,
                UseMlock = false,
            };
        }
    }

    /// <summary>
    /// KeepAlive duration in seconds for Llama 3 General.
    /// </summary>
    public const string Llama3GeneralKeepAlive = "10m";

    /// <summary>
    /// Gets preset for Llama 3 summarization. Lower temperature and slightly stronger repeat penalty
    /// to encourage concise, deterministic output.
    /// </summary>
    public static RequestOptions Llama3Summarize
    {
        get
        {
            int cores = MachineCapabilities.CpuCores;
            long memMb = MachineCapabilities.TotalMemoryMb;
            int gpus = MachineCapabilities.GpuCount;

            return new RequestOptions
            {
                NumCtx = memMb > 32000 ? 8192 : 4096,
                NumThread = Math.Max(1, cores - 1),
                NumGpu = gpus > 0 ? Math.Min(gpus, 1) : 0,
                MainGpu = 0,
                LowVram = gpus == 0,
                Temperature = 0.3f, // more deterministic summaries
                TopP = 0.9f,
                TopK = 40,
                RepeatPenalty = 1.15f,
                UseMmap = true,
                UseMlock = false,
            };
        }
    }

    /// <summary>
    /// KeepAlive duration in seconds for Llama 3 Summarize.
    /// </summary>
    public const string Llama3SummarizeKeepAlive = "10m";

    /// <summary>
    /// Gets preset for DeepSeek R1 14B (reasoning). Larger context window and exploratory temperature
    /// for deeper chains-of-thought, within conservative system limits.
    /// </summary>
    public static RequestOptions DeepSeekR1_14B_Reason
    {
        get
        {
            int cores = MachineCapabilities.CpuCores;
            long memMb = MachineCapabilities.TotalMemoryMb;
            int gpus = MachineCapabilities.GpuCount;

            return new RequestOptions
            {
                NumCtx = memMb > 64000 ? 12288 : 8192, // reasoning benefits from larger ctx
                NumThread = Math.Max(1, cores - 1),
                NumGpu = gpus > 0 ? Math.Min(gpus, 1) : 0,
                MainGpu = 0,
                LowVram = gpus == 0,
                Temperature = 0.6f, // allow broader exploration for reasoning
                TopP = 0.92f,
                TopK = 50,
                RepeatPenalty = 1.05f,
                UseMmap = true,
                UseMlock = false,
            };
        }
    }

    /// <summary>
    /// KeepAlive duration in seconds for DeepSeek R1 14B.
    /// </summary>
    public const string DeepSeekR114BReasonKeepAlive = "10m";

    /// <summary>
    /// Gets preset for DeepSeek R1 32B (reasoning). Tries to leverage up to 2 GPUs if present
    /// and expands context window with sufficient host memory.
    /// </summary>
    public static RequestOptions DeepSeekR1_32B_Reason
    {
        get
        {
            int cores = MachineCapabilities.CpuCores;
            long memMb = MachineCapabilities.TotalMemoryMb;
            int gpus = MachineCapabilities.GpuCount;

            return new RequestOptions
            {
                NumCtx = memMb > 96000 ? 16384 : (memMb > 64000 ? 12288 : 8192),
                NumThread = Math.Max(1, cores - 1),
                NumGpu = gpus > 0 ? Math.Min(gpus, 2) : 0, // allow 2 GPUs if available
                MainGpu = 0,
                LowVram = gpus == 0,
                Temperature = 0.55f,
                TopP = 0.92f,
                TopK = 50,
                RepeatPenalty = 1.05f,
                UseMmap = true,
                UseMlock = false,
            };
        }
    }

    /// <summary>
    /// KeepAlive duration in seconds for DeepSeek R1 32B.
    /// </summary>
    public const string DeepSeekR132BReasonKeepAlive = "10m";

    /// <summary>
    /// Gets preset for Mistral 7B (general). Light-weight, suitable for CPU-only or single-GPU setups,
    /// with a modest context window for RAG tasks.
    /// </summary>
    public static RequestOptions Mistral7BGeneral
    {
        get
        {
            int cores = MachineCapabilities.CpuCores;
            long memMb = MachineCapabilities.TotalMemoryMb;
            int gpus = MachineCapabilities.GpuCount;

            return new RequestOptions
            {
                NumCtx = memMb > 16000 ? 4096 : 3072,
                NumThread = Math.Max(1, cores - 1),
                NumGpu = gpus > 0 ? Math.Min(gpus, 1) : 0,
                MainGpu = 0,
                LowVram = gpus == 0,
                Temperature = 0.5f,
                TopP = 0.9f,
                TopK = 40,
                RepeatPenalty = 1.1f,
                UseMmap = true,
                UseMlock = false,
            };
        }
    }

    /// <summary>
    /// KeepAlive duration in seconds for Mistral 7B General.
    /// </summary>
    public const string Mistral7BGeneralKeepAlive = "10m";

    /// <summary>
    /// Gets preset for Qwen2.5 7B (general). Balanced configuration for mixed tasks and RAG.
    /// </summary>
    public static RequestOptions Qwen25_7B_General
    {
        get
        {
            int cores = MachineCapabilities.CpuCores;
            long memMb = MachineCapabilities.TotalMemoryMb;
            int gpus = MachineCapabilities.GpuCount;

            return new RequestOptions
            {
                NumCtx = memMb > 16000 ? 4096 : 3072,
                NumThread = Math.Max(1, cores - 1),
                NumGpu = gpus > 0 ? Math.Min(gpus, 1) : 0,
                MainGpu = 0,
                LowVram = gpus == 0,
                Temperature = 0.45f,
                TopP = 0.9f,
                TopK = 40,
                RepeatPenalty = 1.1f,
                UseMmap = true,
                UseMlock = false,
            };
        }
    }

    /// <summary>
    /// KeepAlive duration in seconds for Qwen2.5 7B General.
    /// </summary>
    public const string Qwen257BGeneralKeepAlive = "10m";

    /// <summary>
    /// Gets preset for Phi-3 Mini (general). Small footprint model preset for quick local runs.
    /// </summary>
    public static RequestOptions Phi3MiniGeneral
    {
        get
        {
            int cores = MachineCapabilities.CpuCores;
            int gpus = MachineCapabilities.GpuCount;

            return new RequestOptions
            {
                NumCtx = 4096,
                NumThread = Math.Max(1, cores - 1),
                NumGpu = gpus > 0 ? Math.Min(gpus, 1) : 0,
                MainGpu = 0,
                LowVram = gpus == 0,
                Temperature = 0.5f,
                TopP = 0.9f,
                TopK = 40,
                RepeatPenalty = 1.1f,
                UseMmap = true,
                UseMlock = false,
            };
        }
    }

    /// <summary>
    /// KeepAlive duration in seconds for Phi-3 Mini General.
    /// </summary>
    public const string Phi3MiniGeneralKeepAlive = "10m";

    /// <summary>
    /// Gets preset for TinyLlama optimized for high-performance parallel execution.
    /// Designed for divide-and-conquer strategies where multiple instances run concurrently.
    /// Ultra-low context window and aggressive threading for maximum throughput.
    /// </summary>
    public static RequestOptions TinyLlamaFast
    {
        get
        {
            int cores = MachineCapabilities.CpuCores;
            int gpus = MachineCapabilities.GpuCount;

            // Reserve threads for parallel execution - each instance gets fewer threads
            // to allow multiple instances to run simultaneously
            int threadsPerInstance = Math.Max(1, cores / 4);

            return new RequestOptions
            {
                NumCtx = 2048, // Small context for speed
                NumThread = threadsPerInstance,
                NumGpu = gpus > 0 ? 1 : 0, // Single GPU if available
                MainGpu = 0,
                LowVram = true, // Always use low VRAM mode for parallel efficiency
                Temperature = 0.4f, // Lower temperature for more deterministic parallel results
                TopP = 0.85f,
                TopK = 30,
                RepeatPenalty = 1.1f,
                UseMmap = true,
                UseMlock = false,
            };
        }
    }

    /// <summary>
    /// KeepAlive duration in seconds for TinyLlama Fast.
    /// </summary>
    public const string TinyLlamaFastKeepAlive = "5m";

    /// <summary>
    /// Gets preset for Qwen 3.5 0.8B optimized for MeTTa symbolic reasoning.
    /// Ultra-fast, tiny footprint — ideal for atom space queries, pattern matching,
    /// and neural-symbolic bridge operations. Runs entirely in VRAM on any GPU.
    /// </summary>
    public static RequestOptions Qwen35_08B_Symbolic
    {
        get
        {
            int cores = MachineCapabilities.CpuCores;
            int gpus = MachineCapabilities.GpuCount;

            return new RequestOptions
            {
                NumCtx = 4096,    // Sufficient for symbolic queries and MeTTa expressions
                NumThread = Math.Max(1, cores / 4),  // Light threading — model is GPU-bound
                NumGpu = gpus > 0 ? 1 : 0,
                MainGpu = 0,
                LowVram = false,  // 0.8B fits entirely in VRAM — no offloading needed
                Temperature = 0.1f,  // Very low — symbolic reasoning requires precision
                TopP = 0.85f,
                TopK = 20,
                RepeatPenalty = 1.15f,
                UseMmap = true,
                UseMlock = false,
            };
        }
    }

    /// <summary>
    /// KeepAlive duration for Qwen 3.5 0.8B Symbolic. Kept warm for fast atom space queries.
    /// </summary>
    public const string Qwen3508BSymbolicKeepAlive = "10m";
}
