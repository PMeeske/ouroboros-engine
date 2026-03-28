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
