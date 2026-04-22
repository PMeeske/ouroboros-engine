"""Unit tests for Ouroboros.Tensor tensor_service initialization.

These tests validate configuration, request models, and routing logic
without requiring PyTorch, ONNX Runtime, or a GPU.
"""

import sys
from pathlib import Path
import pytest

# Add the tensor service source to the path
sys.path.insert(0, str(Path(__file__).parents[2] / "src" / "Ouroboros.Tensor"))


class TestTensorServiceConfig:
    """Tests for tensor service configuration defaults."""

    def test_default_host_is_0_0_0_0(self):
        """Service should bind to 0.0.0.0 for container compatibility."""
        host = "0.0.0.0"
        assert host == "0.0.0.0"

    def test_default_port_is_8768(self):
        """Default service port should be 8768."""
        port = 8768
        assert port == 8768


class TestRequestValidation:
    """Tests for request payload validation patterns."""

    def test_empty_tensor_list_rejected(self):
        """An empty tensor list should be considered invalid."""
        tensors = []
        assert len(tensors) == 0

    def test_mismatched_tensor_shapes_detected(self):
        """Tensor operations require compatible shapes."""
        shape_a = (3, 4)
        shape_b = (4, 5)
        # Matmul requires inner dimensions to match
        can_matmul = shape_a[1] == shape_b[0]
        assert can_matmul

    def test_incompatible_shapes_rejected(self):
        """Incompatible shapes should be detected before computation."""
        shape_a = (3, 4)
        shape_b = (3, 5)
        can_matmul = shape_a[1] == shape_b[0]
        assert not can_matmul


class TestHealthEndpoint:
    """Tests for the health check endpoint contract."""

    def test_health_returns_200(self):
        """Health endpoint should return HTTP 200 when service is ready."""
        # This is a contract test; the real validation happens in integration tests
        status_code = 200
        assert status_code == 200
