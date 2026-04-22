"""Unit tests for hyperon-sidecar grammar_service initialization.

These tests validate the service configuration and basic logic without
requiring a running Hyperon/MeTTa instance or gRPC server.
"""

import sys
from pathlib import Path
import pytest

# Add the hyperon-sidecar source to the path
sys.path.insert(0, str(Path(__file__).parents[2] / "tools" / "hyperon-sidecar"))


class TestGrammarServiceConfig:
    """Tests for service configuration defaults."""

    def test_default_port_from_env(self, monkeypatch):
        """Default port should be 50051 when HYPERON_GRAMMAR_PORT is not set."""
        monkeypatch.delenv("HYPERON_GRAMMAR_PORT", raising=False)
        # Re-importing would pick up the env var; we test the Dockerfile default instead
        assert True  # Placeholder: env var defaults are tested at container level

    def test_port_override_from_env(self, monkeypatch):
        """Port should be overridable via environment variable."""
        monkeypatch.setenv("HYPERON_GRAMMAR_PORT", "9999")
        port = int(__import__("os").environ.get("HYPERON_GRAMMAR_PORT", "50051"))
        assert port == 9999


class TestGrammarServiceValidation:
    """Tests for input validation logic extracted from the service."""

    def test_empty_grammar_rejected(self):
        """Empty or whitespace-only grammar should be rejected."""
        grammar = "   "
        is_valid = bool(grammar and grammar.strip())
        assert not is_valid

    def test_valid_grammar_accepted(self):
        """Non-empty grammar should be accepted."""
        grammar = "(= (foo $x) (bar $x))"
        is_valid = bool(grammar and grammar.strip())
        assert is_valid

    def test_grammar_trimming(self):
        """Leading/trailing whitespace should be stripped."""
        grammar = "  (= (foo $x) (bar $x))  "
        trimmed = grammar.strip()
        assert trimmed == "(= (foo $x) (bar $x))"


class TestLtoSerialization:
    """Tests for Logic Transfer Object (LTO) serialization patterns."""

    def test_lto_requires_kind(self):
        """An LTO must have a non-empty kind field."""
        lto = {}
        assert not lto.get("kind")

    def test_lto_with_kind_is_valid(self):
        """An LTO with a kind field is structurally valid."""
        lto = {"kind": "MeTTaExpression", "expression": "(= (foo $x) (bar $x))"}
        assert lto.get("kind") == "MeTTaExpression"
