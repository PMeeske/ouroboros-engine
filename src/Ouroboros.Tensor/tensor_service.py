"""
Ouroboros Tensor Service — FastAPI service exposing GPU-accelerated tensor operations.
Runs in Docker with PyTorch ROCm for AMD GPU support.
Endpoints mirror the C# TensorServiceClient contract (RemoteTensorBackend).
"""

from __future__ import annotations

import logging
from typing import List, Optional

import torch
import torch.nn.functional as F
from fastapi import FastAPI, HTTPException, Query
from pydantic import BaseModel

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

app = FastAPI(title="Ouroboros Tensor Service", version="0.1.0")

# Determine device once at startup
_device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
logger.info("Tensor service starting. Device: %s", _device)

# ── Fish Speech TTS state (loaded lazily on first /tts call) ─────────────────
_fish_tts: Optional["FishTTSEngine"] = None


# ── Pydantic models (field names match C# JsonPropertyName attributes) ──────

class TensorData(BaseModel):
    shape: List[int]
    data: List[float]


class MatMulRequest(BaseModel):
    a: TensorData
    b: TensorData


class TensorDataResponse(BaseModel):
    shape: List[int]
    data: List[float]


class MatMulResponse(BaseModel):
    result: TensorDataResponse


class FftRequest(BaseModel):
    input: TensorData


class FftResponse(BaseModel):
    result: TensorDataResponse


class CosineSimilarityRequest(BaseModel):
    a: TensorData
    b: TensorData


class CosineSimilarityResponse(BaseModel):
    similarity: float


class HealthResponse(BaseModel):
    status: str
    device: str


# ── Endpoints ─────────────────────────────────────────────────────────────────

@app.get("/health", response_model=HealthResponse)
def health() -> HealthResponse:
    """Health check. Returns device type: 'cuda' when ROCm GPU available, 'cpu' otherwise."""
    device_str = "cuda" if torch.cuda.is_available() else "cpu"
    return HealthResponse(status="healthy", device=device_str)


@app.post("/tensor/matmul", response_model=MatMulResponse)
@torch.no_grad()
def matmul(request: MatMulRequest) -> MatMulResponse:
    """
    Matrix multiplication: result = A @ B.
    A shape [M, K], B shape [K, N] → result shape [M, N].
    Shapes must be 2D and inner dimensions must match.
    """
    a_shape = request.a.shape
    b_shape = request.b.shape

    if len(a_shape) != 2 or len(b_shape) != 2:
        raise HTTPException(status_code=400, detail=f"MatMul requires 2D tensors. Got shapes {a_shape} and {b_shape}.")
    if a_shape[1] != b_shape[0]:
        raise HTTPException(
            status_code=400,
            detail=f"MatMul inner dimensions must match. Got {a_shape[1]} vs {b_shape[0]}."
        )

    a = torch.tensor(request.a.data, dtype=torch.float32, device=_device).reshape(a_shape)
    b = torch.tensor(request.b.data, dtype=torch.float32, device=_device).reshape(b_shape)
    result = torch.matmul(a, b)

    result_data = TensorDataResponse(shape=list(result.shape), data=result.cpu().flatten().tolist())
    return MatMulResponse(result=result_data)


@app.post("/tensor/fft", response_model=FftResponse)
@torch.no_grad()
def fft(request: FftRequest, dimensions: int = Query(default=1)) -> FftResponse:
    """
    Real FFT of a 1D signal. Returns interleaved real/imaginary pairs.
    Input shape [N] → output shape [N, 2] (real/imag pairs as flat float array of length 2*N).
    The C# RemoteTensorBackend reads pairs: data[2i] = real, data[2i+1] = imag.
    The 'dimensions' query parameter is accepted for API compatibility but only 1D FFT is supported.
    """
    input_shape = request.input.shape
    if len(input_shape) != 1:
        raise HTTPException(status_code=400, detail=f"FFT requires 1D input. Got shape {input_shape}.")

    signal = torch.tensor(request.input.data, dtype=torch.float32, device=_device)
    spectrum = torch.fft.fft(signal)  # complex64 output

    # Interleave real and imaginary as the C# client expects float pairs
    real = spectrum.real.cpu()
    imag = spectrum.imag.cpu()
    interleaved = torch.stack([real, imag], dim=1).flatten()

    # Output shape encodes [N, 2] so C# can reconstruct complex values
    result_data = TensorDataResponse(shape=[input_shape[0], 2], data=interleaved.tolist())
    return FftResponse(result=result_data)


@app.post("/tensor/cosine_similarity", response_model=CosineSimilarityResponse)
@torch.no_grad()
def cosine_similarity(request: CosineSimilarityRequest) -> CosineSimilarityResponse:
    """
    Cosine similarity between two 1D vectors. Returns scalar in [-1, 1].
    """
    if len(request.a.shape) != 1 or len(request.b.shape) != 1:
        raise HTTPException(status_code=400, detail="cosine_similarity requires 1D vectors.")
    if request.a.shape[0] != request.b.shape[0]:
        raise HTTPException(
            status_code=400,
            detail=f"Vector lengths must match. Got {request.a.shape[0]} vs {request.b.shape[0]}."
        )

    a = torch.tensor(request.a.data, dtype=torch.float32, device=_device).unsqueeze(0)
    b = torch.tensor(request.b.data, dtype=torch.float32, device=_device).unsqueeze(0)
    sim = F.cosine_similarity(a, b, dim=1).item()

    return CosineSimilarityResponse(similarity=float(sim))


# ── Training loss / step models ──────────────────────────────────────────────

class TrainingLossRequest(BaseModel):
    predicted: List[float]          # RGB [0,1] normalized, H*W*3 flat
    ground_truth: List[float]       # RGB [0,1] normalized, H*W*3 flat
    width: int
    height: int
    face_mask: Optional[List[float]] = None  # H*W [0,1] optional weighting mask


class TrainingLossResponse(BaseModel):
    l1_loss: float
    ssim_loss: float
    total_loss: float


class TrainingStepRequest(BaseModel):
    lora_weights: List[float]       # flat LoRA weight array
    gradient: List[float]           # same shape as weights — pre-computed gradient
    learning_rate: float = 0.001


class TrainingStepResponse(BaseModel):
    updated_weights: List[float]


# ── Training loss / step endpoints ───────────────────────────────────────────

def _gaussian_window(size: int, sigma: float, device: torch.device) -> torch.Tensor:
    """Create 2D Gaussian window for SSIM computation."""
    coords = torch.arange(size, dtype=torch.float32, device=device) - (size - 1) / 2.0
    g = torch.exp(-(coords ** 2) / (2.0 * sigma ** 2))
    g = g / g.sum()
    window = g.unsqueeze(1) @ g.unsqueeze(0)  # outer product → 2D
    return window.unsqueeze(0).unsqueeze(0)     # (1, 1, size, size)


def _ssim_channel(x: torch.Tensor, y: torch.Tensor, window: torch.Tensor,
                  k1: float = 0.01, k2: float = 0.03) -> torch.Tensor:
    """Compute SSIM for a single channel. x, y shape (1, 1, H, W)."""
    c1 = k1 ** 2
    c2 = k2 ** 2
    pad = window.shape[-1] // 2

    mu_x = F.conv2d(x, window, padding=pad)
    mu_y = F.conv2d(y, window, padding=pad)
    mu_x_sq = mu_x ** 2
    mu_y_sq = mu_y ** 2
    mu_xy = mu_x * mu_y

    sigma_x_sq = F.conv2d(x ** 2, window, padding=pad) - mu_x_sq
    sigma_y_sq = F.conv2d(y ** 2, window, padding=pad) - mu_y_sq
    sigma_xy = F.conv2d(x * y, window, padding=pad) - mu_xy

    numerator = (2.0 * mu_xy + c1) * (2.0 * sigma_xy + c2)
    denominator = (mu_x_sq + mu_y_sq + c1) * (sigma_x_sq + sigma_y_sq + c2)
    return (numerator / denominator).mean()


@app.post("/training/loss", response_model=TrainingLossResponse)
@torch.no_grad()
def training_loss(request: TrainingLossRequest) -> TrainingLossResponse:
    """Compute L1 + SSIM loss between predicted and ground-truth images on GPU.

    Accepts RGB float arrays in [0, 1], returns L1, SSIM, and weighted total loss.
    Total loss = 0.8 * L1 + 0.2 * (1 - SSIM).
    """
    h, w = request.height, request.width
    expected_len = h * w * 3
    if len(request.predicted) != expected_len or len(request.ground_truth) != expected_len:
        raise HTTPException(
            status_code=400,
            detail=f"Expected {expected_len} pixels (H={h} W={w} * 3), "
                   f"got predicted={len(request.predicted)}, gt={len(request.ground_truth)}",
        )

    pred = torch.tensor(request.predicted, dtype=torch.float32, device=_device).reshape(1, h, w, 3).permute(0, 3, 1, 2)
    gt = torch.tensor(request.ground_truth, dtype=torch.float32, device=_device).reshape(1, h, w, 3).permute(0, 3, 1, 2)

    # Apply face mask if provided
    if request.face_mask is not None:
        if len(request.face_mask) != h * w:
            raise HTTPException(status_code=400, detail=f"face_mask length {len(request.face_mask)} != {h * w}")
        mask = torch.tensor(request.face_mask, dtype=torch.float32, device=_device).reshape(1, 1, h, w)
        mask = mask.expand_as(pred)  # broadcast to (1, 3, H, W)
        l1 = (torch.abs(pred - gt) * mask).sum() / mask.sum().clamp(min=1.0)
    else:
        l1 = torch.mean(torch.abs(pred - gt))

    # SSIM — compute per channel and average
    window = _gaussian_window(11, 1.5, _device)
    ssim_val = torch.tensor(0.0, device=_device)
    for c in range(3):
        ssim_val = ssim_val + _ssim_channel(
            pred[:, c:c+1, :, :], gt[:, c:c+1, :, :], window,
        )
    ssim_val = ssim_val / 3.0

    total = 0.8 * l1 + 0.2 * (1.0 - ssim_val)

    return TrainingLossResponse(
        l1_loss=float(l1),
        ssim_loss=float(ssim_val),
        total_loss=float(total),
    )


@app.post("/training/step", response_model=TrainingStepResponse)
@torch.no_grad()
def training_step(request: TrainingStepRequest) -> TrainingStepResponse:
    """Apply a single SGD gradient step on GPU.

    Accepts LoRA weights and pre-computed gradient (same shape), returns updated weights.
    updated = weights - learning_rate * gradient.
    """
    if len(request.lora_weights) != len(request.gradient):
        raise HTTPException(
            status_code=400,
            detail=f"Weight/gradient length mismatch: {len(request.lora_weights)} vs {len(request.gradient)}",
        )

    weights = torch.tensor(request.lora_weights, dtype=torch.float32, device=_device)
    grad = torch.tensor(request.gradient, dtype=torch.float32, device=_device)
    updated = weights - request.learning_rate * grad

    return TrainingStepResponse(updated_weights=updated.cpu().tolist())


# ── EWC (Elastic Weight Consolidation) models ────────────────────────────────

class FisherRequest(BaseModel):
    weights: List[float]              # 65536 elements (256x256 flat row-major)
    training_inputs: List[List[float]]  # batch of 256-dim vectors
    training_outputs: List[List[float]] # corresponding 256-dim target vectors


class FisherResponse(BaseModel):
    fisher_diagonal: List[float]      # 65536 elements
    sample_count: int


class AnchorRequest(BaseModel):
    weights: List[float]              # 65536 elements — anchor weight snapshot


class PenaltyRequest(BaseModel):
    current_weights: List[float]      # 65536 elements
    lambda_value: float               # EWC lambda (1000 for identity, 100 emotional, 10 expression)


class PenaltyResponse(BaseModel):
    penalty: float
    max_drift: float                  # max absolute weight change from anchor


class DriftRequest(BaseModel):
    current_weights: List[float]      # 65536 elements
    threshold: float                  # max allowed drift


class DriftResponse(BaseModel):
    within_threshold: bool
    max_drift: float
    mean_drift: float


# ── EWC module-level state ────────────────────────────────────────────────────

_ewc_fisher: Optional[torch.Tensor] = None
_ewc_anchor: Optional[torch.Tensor] = None


# ── EWC Endpoints ─────────────────────────────────────────────────────────────

@app.post("/ewc/compute_fisher", response_model=FisherResponse)
def compute_fisher(request: FisherRequest) -> FisherResponse:
    """Compute Fisher Information diagonal via PyTorch autograd.

    Accepts weights (256x256 flat), training inputs and outputs.
    Returns normalized Fisher diagonal in [0, 1] range.
    """
    global _ewc_fisher
    logger.info("EWC compute_fisher: %d weights, %d samples", len(request.weights), len(request.training_inputs))

    weights = torch.tensor(request.weights, dtype=torch.float32, device=_device).reshape(256, 256)
    inputs = [torch.tensor(x, dtype=torch.float32, device=_device) for x in request.training_inputs]
    outputs = [torch.tensor(y, dtype=torch.float32, device=_device) for y in request.training_outputs]

    fisher = torch.zeros(256, 256, dtype=torch.float32, device=_device)

    for x, y in zip(inputs, outputs):
        w = weights.clone().detach().requires_grad_(True)
        pred = w @ x
        loss = F.mse_loss(pred, y)
        loss.backward()
        fisher += w.grad ** 2

    sample_count = len(inputs)
    if sample_count > 0:
        fisher = fisher / sample_count

    # Normalize to [0, 1] range
    fisher_max = fisher.max()
    if fisher_max > 0:
        fisher = fisher / fisher.max()

    _ewc_fisher = fisher.detach()

    return FisherResponse(
        fisher_diagonal=fisher.flatten().tolist(),
        sample_count=sample_count,
    )


@app.post("/ewc/store_anchor")
def store_anchor(request: AnchorRequest) -> dict:
    """Store anchor weights for EWC comparison."""
    global _ewc_anchor
    logger.info("EWC store_anchor: %d weights", len(request.weights))

    anchor = torch.tensor(request.weights, dtype=torch.float32, device=_device).reshape(256, 256)
    _ewc_anchor = anchor
    return {"status": "ok", "anchor_norm": float(anchor.norm())}


@app.post("/ewc/compute_penalty", response_model=PenaltyResponse)
def compute_penalty(request: PenaltyRequest) -> PenaltyResponse:
    """Compute EWC penalty: lambda * sum(fisher * (current - anchor)^2)."""
    if _ewc_fisher is None or _ewc_anchor is None:
        raise HTTPException(status_code=400, detail="Fisher or anchor not initialized")

    logger.info("EWC compute_penalty: lambda=%.1f", request.lambda_value)

    current = torch.tensor(request.current_weights, dtype=torch.float32, device=_device).reshape(256, 256)
    diff = current - _ewc_anchor
    penalty = (_ewc_fisher * diff ** 2).sum() * request.lambda_value

    return PenaltyResponse(
        penalty=float(penalty),
        max_drift=float(diff.abs().max()),
    )


@app.get("/ewc/status")
def ewc_status() -> dict:
    """Health check for EWC state."""
    logger.info("EWC status check")
    return {
        "fisher_initialized": _ewc_fisher is not None,
        "anchor_initialized": _ewc_anchor is not None,
        "fisher_shape": list(_ewc_fisher.shape) if _ewc_fisher is not None else None,
        "device": str(_device),
    }


# ── Fish Speech TTS Engine ───────────────────────────────────────────────────

class TTSRequest(BaseModel):
    text: str
    reference_id: Optional[str] = None
    max_new_tokens: int = 1024
    chunk_length: int = 300
    top_p: float = 0.8
    temperature: float = 0.8
    repetition_penalty: float = 1.1
    format: str = "wav"


class TTSGenerateRequest(BaseModel):
    """Request for LLAMA semantic token generation only (no DAC decode)."""
    text: str
    max_new_tokens: int = 1024
    chunk_length: int = 300
    top_p: float = 0.8
    temperature: float = 0.8
    repetition_penalty: float = 1.1


class FishTTSEngine:
    """Wraps Fish Speech LLAMA + DAC for GPU-isolated TTS inference."""

    def __init__(self, checkpoint_path: str, dac_onnx_path: str, device: torch.device):
        import sys
        import os
        # Add fish-speech to path
        fish_dir = os.environ.get("FISH_SPEECH_DIR", "/app/fish-speech")
        if os.path.isdir(fish_dir) and fish_dir not in sys.path:
            sys.path.insert(0, fish_dir)

        from fish_speech.models.text2semantic.inference import (
            init_model, generate, encode_audio,
        )
        from fish_speech.tokenizer import FishTokenizer
        from fish_speech.conversation import Conversation, Message
        from fish_speech.utils.schema import ServeTTSRequest

        self.device = device
        self.generate = generate
        self.ServeTTSRequest = ServeTTSRequest
        self.Conversation = Conversation
        self.Message = Message

        # Load LLAMA on GPU
        logger.info("Loading Fish Speech LLAMA from %s on %s", checkpoint_path, device)
        self.model, self.decode_one_token = init_model(
            checkpoint_path=checkpoint_path,
            device=str(device),
            precision=torch.half,
            compile=False,
        )
        self.tokenizer = FishTokenizer(os.path.join(checkpoint_path, "tokenizer.json"))
        logger.info("Fish Speech LLAMA loaded")
        logger.info(
            "LLAMA KV cache: %d layers, model dim %d",
            self.model.config.num_layers,
            self.model.config.dim,
        )

        # Load DAC decoder via ONNX (CPU -- avoids MIOpen workspace issues)
        import onnxruntime as ort
        self.dac_session = ort.InferenceSession(
            dac_onnx_path,
            providers=["CPUExecutionProvider"],
        )
        logger.info("Fish Speech DAC ONNX loaded from %s", dac_onnx_path)

    @torch.no_grad()
    def generate_codes(
        self,
        text: str,
        max_new_tokens: int = 1024,
        chunk_length: int = 300,
        top_p: float = 0.8,
        temperature: float = 0.8,
        repetition_penalty: float = 1.1,
    ) -> tuple:
        """Run LLAMA text-to-semantic inference only (no DAC decode).

        Returns:
            (codes_np: ndarray[int64] shape [num_codebooks, gen_len], generation_time_ms: float)
        """
        import numpy as np
        import time

        from fish_speech.conversation import Conversation, Message

        conv = Conversation(messages=[
            Message(role="system", content="convert the provided text to speech"),
            Message(role="user", content=text),
        ])

        prompt = conv.encode_for_inference(
            tokenizer=self.tokenizer,
            num_codebooks=self.model.config.num_codebooks,
        )
        prompt_tokens = prompt["tokens"].to(self.device)
        prompt_length = prompt_tokens.shape[-1]

        t0 = time.perf_counter()
        all_codes = self.generate(
            model=self.model,
            prompt=prompt_tokens,
            max_new_tokens=max_new_tokens,
            encode_func=self.decode_one_token,
            decode_func=self.decode_one_token,
            temperature=temperature,
            top_p=top_p,
            repetition_penalty=repetition_penalty,
            iterative_prompt=chunk_length > 0,
            chunk_length=chunk_length,
        )
        generation_time_ms = (time.perf_counter() - t0) * 1000

        codes = all_codes[1:, prompt_length:-1]  # [num_codebooks, gen_len]

        if codes.shape[1] == 0:
            raise ValueError("LLAMA generated 0 semantic tokens")

        codes_np = codes.cpu().numpy().astype(np.int64)
        logger.info(
            "generate_codes: %d codebooks x %d tokens in %.1fms",
            codes_np.shape[0], codes_np.shape[1], generation_time_ms,
        )
        return codes_np, generation_time_ms

    @torch.no_grad()
    def synthesize(self, text: str, max_new_tokens: int = 1024,
                   chunk_length: int = 300, top_p: float = 0.8,
                   temperature: float = 0.8, repetition_penalty: float = 1.1) -> bytes:
        """Generate WAV audio from text. Returns raw WAV bytes."""
        import numpy as np
        import io
        import wave as wave_mod

        from fish_speech.conversation import Conversation, Message

        # Build conversation
        conv = Conversation(messages=[
            Message(role="system", content="convert the provided text to speech"),
            Message(role="user", content=text),
        ])

        # Encode for model
        prompt = conv.encode_for_inference(
            tokenizer=self.tokenizer,
            num_codebooks=self.model.config.num_codebooks,
        )
        prompt_tokens = prompt["tokens"].to(self.device)
        prompt_length = prompt_tokens.shape[-1]

        # Generate codes with LLAMA on GPU
        all_codes = self.generate(
            model=self.model,
            prompt=prompt_tokens,
            max_new_tokens=max_new_tokens,
            encode_func=self.decode_one_token,
            decode_func=self.decode_one_token,
            temperature=temperature,
            top_p=top_p,
            repetition_penalty=repetition_penalty,
            iterative_prompt=chunk_length > 0,
            chunk_length=chunk_length,
        )

        # Extract generated codes (skip prompt, remove eos)
        codes = all_codes[1:, prompt_length:-1]  # [num_codebooks, gen_len]

        if codes.shape[1] == 0:
            logger.warning("Fish Speech generated 0 tokens")
            return b""

        # Decode codes to audio via ONNX DAC (CPU)
        codes_np = codes.cpu().numpy().astype(np.int64)
        codes_np = codes_np[np.newaxis, :, :]  # [1, num_codebooks, time]
        audio_out = self.dac_session.run(None, {"codes": codes_np})[0]
        audio_float = audio_out.squeeze()  # [audio_samples]

        # Convert to 16-bit PCM WAV
        audio_int16 = np.clip(audio_float * 32767, -32768, 32767).astype(np.int16)

        buf = io.BytesIO()
        with wave_mod.open(buf, "wb") as wf:
            wf.setnchannels(1)
            wf.setsampwidth(2)
            wf.setframerate(44100)
            wf.writeframes(audio_int16.tobytes())

        return buf.getvalue()


def _get_fish_tts() -> FishTTSEngine:
    """Lazy-load Fish Speech TTS engine on first call."""
    global _fish_tts
    if _fish_tts is None:
        import os
        checkpoint = os.environ.get("FISH_CHECKPOINT", "checkpoints/openaudio-s1-mini")
        dac_onnx = os.environ.get("FISH_DAC_ONNX", "checkpoints/onnx/dac_decode.onnx")
        _fish_tts = FishTTSEngine(checkpoint, dac_onnx, _device)
    return _fish_tts


@app.post("/tts")
def tts(request: TTSRequest):
    """Synthesize speech from text using Fish Speech (GPU LLAMA + ONNX DAC)."""
    from fastapi.responses import Response

    engine = _get_fish_tts()
    wav_bytes = engine.synthesize(
        text=request.text,
        max_new_tokens=request.max_new_tokens,
        chunk_length=request.chunk_length,
        top_p=request.top_p,
        temperature=request.temperature,
        repetition_penalty=request.repetition_penalty,
    )

    if not wav_bytes:
        raise HTTPException(status_code=500, detail="TTS generated empty audio")

    return Response(content=wav_bytes, media_type="audio/wav")


@app.post("/tts/generate")
def tts_generate(request: TTSGenerateRequest):
    """Generate semantic tokens via LLAMA only (no DAC decode).

    Returns VQ codes tensor [num_codebooks, gen_len] with timing metadata.
    """
    engine = _get_fish_tts()
    codes_np, generation_time_ms = engine.generate_codes(
        text=request.text,
        max_new_tokens=request.max_new_tokens,
        chunk_length=request.chunk_length,
        top_p=request.top_p,
        temperature=request.temperature,
        repetition_penalty=request.repetition_penalty,
    )
    num_codebooks = int(codes_np.shape[0])
    gen_length = int(codes_np.shape[1])
    return {
        "codes": codes_np.tolist(),
        "num_codebooks": num_codebooks,
        "gen_length": gen_length,
        "generation_time_ms": round(generation_time_ms, 2),
    }


@app.get("/tts/health")
def tts_health():
    """Check if Fish Speech TTS is loaded. Returns model config when available."""
    if _fish_tts is not None:
        model_cfg = _fish_tts.model.config
        return {
            "loaded": True,
            "model_loaded": True,
            "device": str(_device),
            "model_config": {
                "num_codebooks": model_cfg.num_codebooks,
                "vocab_size": model_cfg.vocab_size,
                "dim": model_cfg.dim,
            },
        }
    return {"loaded": False, "model_loaded": False, "device": str(_device)}


@app.get("/tts/test_generate")
def tts_test_generate():
    """Diagnostic endpoint: verify LLAMA inference produces valid semantic tokens.

    Runs generate_codes with a test phrase and validates code ranges.
    This is a diagnostic endpoint for development verification only.
    """
    engine = _get_fish_tts()
    codes_np, generation_time_ms = engine.generate_codes("Hello, this is a test.")

    num_codebooks = int(codes_np.shape[0])
    gen_length = int(codes_np.shape[1])
    code_min = int(codes_np.min())
    code_max = int(codes_np.max())
    tokens_per_second = gen_length / (generation_time_ms / 1000) if generation_time_ms > 0 else 0

    # At 44100 Hz with ~512 samples per code, real-time threshold ~ 86 codes/sec
    realtime_threshold = 44100 / 512
    exceeds_realtime = tokens_per_second > realtime_threshold
    logger.info(
        "test_generate: %d tokens at %.1f tok/s (realtime=%.1f, exceeds=%s)",
        gen_length, tokens_per_second, realtime_threshold, exceeds_realtime,
    )

    if code_max > 4095:
        raise HTTPException(
            status_code=500,
            detail=f"Codes out of codebook range: max={code_max} > 4095",
        )

    return {
        "success": True,
        "num_codebooks": num_codebooks,
        "gen_length": gen_length,
        "code_range": [code_min, code_max],
        "generation_time_ms": round(generation_time_ms, 2),
        "tokens_per_second": round(tokens_per_second, 2),
        "exceeds_realtime": exceeds_realtime,
    }


@app.post("/ewc/validate_drift", response_model=DriftResponse)
def validate_drift(request: DriftRequest) -> DriftResponse:
    """Check if weight drift from anchor exceeds threshold."""
    if _ewc_anchor is None:
        raise HTTPException(status_code=400, detail="Anchor not initialized")

    logger.info("EWC validate_drift: threshold=%.4f", request.threshold)

    current = torch.tensor(request.current_weights, dtype=torch.float32, device=_device).reshape(256, 256)
    diff = current - _ewc_anchor
    max_drift = float(diff.abs().max())
    mean_drift = float(diff.abs().mean())

    return DriftResponse(
        within_threshold=max_drift <= request.threshold,
        max_drift=max_drift,
        mean_drift=mean_drift,
    )
