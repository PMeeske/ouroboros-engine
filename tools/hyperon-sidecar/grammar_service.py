"""
Hyperon Grammar Service — gRPC sidecar for grammar validation, correction, and composition.

This service wraps the upstream Hyperon/MeTTa engine and exposes grammar operations
to the .NET Ouroboros Engine via gRPC.
"""

from __future__ import annotations

import logging
import os
import re
import uuid
from concurrent import futures
from pathlib import Path

import grpc
from grpc_reflection.v1alpha import reflection

from hyperon import MeTTa, Atom, ExpressionAtom, SymbolAtom

import hyperon_grammar_pb2 as pb2
import hyperon_grammar_pb2_grpc as pb2_grpc

logger = logging.getLogger(__name__)

GRAMMAR_ATOMS_PATH = Path(__file__).parent / "grammar_atoms.metta"
VERSION = "0.1.0"


class GrammarAtomSpace:
    """Manages the MeTTa space for grammar atoms and proven grammar storage."""

    def __init__(self) -> None:
        self._metta = MeTTa()
        self._proven_grammars: dict[str, tuple[str, str]] = {}  # id -> (desc, g4)
        self._load_grammar_atoms()

    def _load_grammar_atoms(self) -> None:
        """Load the grammar validation/correction rules from grammar_atoms.metta."""
        if GRAMMAR_ATOMS_PATH.exists():
            metta_source = GRAMMAR_ATOMS_PATH.read_text(encoding="utf-8")
            try:
                self._metta.run(metta_source)
                logger.info("Loaded grammar atoms from %s", GRAMMAR_ATOMS_PATH)
            except Exception:
                logger.exception("Failed to load grammar atoms — using empty space")

    def validate_grammar(self, grammar_g4: str) -> tuple[bool, list[dict]]:
        """Validate an ANTLR4 grammar string for structural issues."""
        issues: list[dict] = []

        # Parse the .g4 to extract rule structure
        rules = _parse_g4_rules(grammar_g4)
        if not rules:
            issues.append({
                "severity": pb2.GRAMMAR_ISSUE_SEVERITY_ERROR,
                "rule_name": "",
                "description": "Could not parse any rules from the grammar",
                "kind": pb2.GRAMMAR_ISSUE_KIND_SYNTAX_ERROR,
            })
            return False, issues

        # Check for left recursion
        for name, alternatives in rules.items():
            for alt in alternatives:
                tokens = alt.strip().split()
                if tokens and tokens[0] == name:
                    issues.append({
                        "severity": pb2.GRAMMAR_ISSUE_SEVERITY_ERROR,
                        "rule_name": name,
                        "description": f"Direct left recursion: {name} -> {name} ...",
                        "kind": pb2.GRAMMAR_ISSUE_KIND_LEFT_RECURSION,
                    })
                    break  # one issue per rule

        # Check for unreachable rules
        start_rule = _find_start_rule(grammar_g4, rules)
        if start_rule:
            reachable = _find_reachable(start_rule, rules)
            for name in rules:
                if name not in reachable and not name.isupper():
                    issues.append({
                        "severity": pb2.GRAMMAR_ISSUE_SEVERITY_WARNING,
                        "rule_name": name,
                        "description": f"Rule '{name}' is not reachable from start rule '{start_rule}'",
                        "kind": pb2.GRAMMAR_ISSUE_KIND_UNREACHABLE_RULE,
                    })

        # Check for undefined rule references
        defined = set(rules.keys())
        for name, alternatives in rules.items():
            for alt in alternatives:
                for token in alt.strip().split():
                    # Lowercase tokens are rule refs in ANTLR (parser rules)
                    cleaned = re.sub(r"[?*+()]", "", token)
                    if (
                        cleaned
                        and cleaned[0].islower()
                        and cleaned not in defined
                        and cleaned not in ("true", "false", "null")
                    ):
                        issues.append({
                            "severity": pb2.GRAMMAR_ISSUE_SEVERITY_ERROR,
                            "rule_name": name,
                            "description": f"Rule '{name}' references undefined rule '{cleaned}'",
                            "kind": pb2.GRAMMAR_ISSUE_KIND_MISSING_RULE,
                        })

        is_valid = not any(
            i["severity"] == pb2.GRAMMAR_ISSUE_SEVERITY_ERROR for i in issues
        )
        return is_valid, issues

    def correct_grammar(
        self, grammar_g4: str, known_issues: list[dict]
    ) -> tuple[bool, str, list[str], list[dict]]:
        """Attempt to correct a grammar based on known issues."""
        corrected = grammar_g4
        corrections: list[str] = []

        for issue in known_issues:
            kind = issue.get("kind", 0)
            rule_name = issue.get("rule_name", "")

            if kind == pb2.GRAMMAR_ISSUE_KIND_LEFT_RECURSION and rule_name:
                corrected, applied = _fix_left_recursion(corrected, rule_name)
                if applied:
                    corrections.append(
                        f"Removed left recursion from '{rule_name}'"
                    )

        # Re-validate after corrections
        is_valid, remaining = self.validate_grammar(corrected)
        return True, corrected, corrections, remaining

    def compose_grammars(
        self, fragments: list[str], grammar_name: str
    ) -> tuple[bool, str, list[str]]:
        """Compose multiple grammar fragments into one grammar."""
        if not fragments:
            return False, "", ["No fragments provided"]

        all_rules: dict[str, list[str]] = {}
        all_tokens: dict[str, str] = {}
        conflicts_resolved: list[str] = []

        for i, frag in enumerate(fragments):
            rules = _parse_g4_rules(frag)
            tokens = _parse_g4_tokens(frag)

            for name, alts in rules.items():
                if name in all_rules:
                    # Conflict: merge alternatives
                    existing = set(tuple(a.split()) for a in all_rules[name])
                    for alt in alts:
                        if tuple(alt.split()) not in existing:
                            all_rules[name].append(alt)
                    conflicts_resolved.append(
                        f"Merged alternatives for rule '{name}' from fragment {i}"
                    )
                else:
                    all_rules[name] = list(alts)

            for name, pattern in tokens.items():
                if name not in all_tokens:
                    all_tokens[name] = pattern

        # Build composed grammar
        lines = [f"grammar {grammar_name or 'ComposedGrammar'};", ""]

        # Parser rules first
        for name, alts in all_rules.items():
            if name[0].islower():
                body = "\n    | ".join(alts)
                lines.append(f"{name}\n    : {body}\n    ;")
                lines.append("")

        # Then lexer rules
        for name, alts in all_rules.items():
            if name[0].isupper():
                body = "\n    | ".join(alts)
                lines.append(f"{name}\n    : {body}\n    ;")
                lines.append("")

        for name, pattern in all_tokens.items():
            if name not in all_rules:
                lines.append(f"{name}: {pattern};")

        composed = "\n".join(lines)
        return True, composed, conflicts_resolved

    def refine_grammar(
        self, grammar_g4: str, failure: dict
    ) -> tuple[bool, str, str]:
        """Refine a grammar based on a parse failure."""
        offending = failure.get("offending_token", "")
        expected = failure.get("expected_tokens", "")
        line = failure.get("line", 0)
        snippet = failure.get("input_snippet", "")

        explanation_parts: list[str] = []
        refined = grammar_g4

        if offending and expected:
            # Try to add the offending token as an alternative where expected tokens live
            explanation_parts.append(
                f"Parse failed at line {line}: got '{offending}', "
                f"expected one of [{expected}]."
            )

            # Simple heuristic: if a token literal is missing, add it to the lexer
            if offending.startswith("'") or offending.startswith('"'):
                token_name = f"T_{offending.strip(chr(39)).strip(chr(34)).upper()}"
                if token_name not in refined:
                    refined += f"\n{token_name}: {offending};"
                    explanation_parts.append(
                        f"Added token rule {token_name} for literal {offending}."
                    )

        if snippet:
            explanation_parts.append(
                f"Input context: '{snippet[:100]}'"
            )

        explanation = " ".join(explanation_parts) if explanation_parts else "No refinement applied."
        success = refined != grammar_g4
        return success, refined, explanation

    def store_proven_grammar(
        self, description: str, grammar_g4: str, sample_inputs: list[str]
    ) -> tuple[bool, str]:
        """Store a proven grammar for future retrieval."""
        gid = str(uuid.uuid4())[:8]
        self._proven_grammars[gid] = (description, grammar_g4)

        # Also store in MeTTa space for symbolic querying
        try:
            escaped_desc = description.replace('"', '\\"')
            escaped_g4 = grammar_g4.replace('"', '\\"').replace("\n", "\\n")
            self._metta.run(f'(ProvenGrammar "{escaped_desc}" "{escaped_g4}")')
        except Exception:
            logger.exception("Failed to store grammar in MeTTa space")

        return True, gid

    def retrieve_grammar(self, description: str) -> tuple[bool, str, str, float]:
        """Retrieve the closest matching proven grammar."""
        if not self._proven_grammars:
            return False, "", "", 0.0

        best_id = ""
        best_score = 0.0
        desc_words = set(description.lower().split())

        for gid, (stored_desc, g4) in self._proven_grammars.items():
            stored_words = set(stored_desc.lower().split())
            if not desc_words or not stored_words:
                continue
            overlap = len(desc_words & stored_words)
            score = overlap / max(len(desc_words | stored_words), 1)
            if score > best_score:
                best_score = score
                best_id = gid

        if best_id and best_score > 0.1:
            desc, g4 = self._proven_grammars[best_id]
            return True, g4, best_id, best_score

        return False, "", "", 0.0

    # =========================================================================
    # Logic Transfer Object (LTO) Operations
    # =========================================================================

    def atoms_to_g4(self, metta_atoms: str) -> tuple[bool, str, list[str]]:
        """Convert MeTTa grammar spec atoms (LTOs) to an ANTLR4 .g4 grammar string.

        The atoms should use MkGrammar, MkProduction, MkTerminal, and MkRegexTerminal
        constructors as defined in grammar_atoms.metta.
        """
        notes: list[str] = []

        try:
            # Parse the MeTTa source into the space
            results = self._metta.run(metta_atoms)
            notes.append(f"Parsed {len(results)} MeTTa expression group(s)")
        except Exception as e:
            return False, "", [f"Failed to parse MeTTa atoms: {e}"]

        # Extract grammar structure by querying the space
        grammar_name, start_rule, productions, terminals = self._extract_grammar_structure(
            metta_atoms, notes
        )

        if not grammar_name:
            return False, "", notes + ["No MkGrammar atom found in the provided atoms"]

        # Build the .g4 output
        g4_lines = [f"grammar {grammar_name};", ""]

        # Parser rules
        for prod_name, alternatives in productions.items():
            if prod_name[0].islower() if prod_name else False:
                alts_str = "\n    | ".join(alternatives)
                g4_lines.append(f"{prod_name}\n    : {alts_str}\n    ;")
                g4_lines.append("")

        # Lexer rules (uppercase)
        for prod_name, alternatives in productions.items():
            if prod_name and prod_name[0].isupper():
                alts_str = "\n    | ".join(alternatives)
                g4_lines.append(f"{prod_name}\n    : {alts_str}\n    ;")
                g4_lines.append("")

        # Explicit terminal definitions
        for term_name, term_pattern in terminals.items():
            if term_name not in productions:
                g4_lines.append(f"{term_name}: {term_pattern};")

        # Add default WS skip rule if not explicitly defined
        has_ws = any(
            name.upper() == "WS" for name in list(productions.keys()) + list(terminals.keys())
        )
        if not has_ws:
            g4_lines.append("")
            g4_lines.append("WS: [ \\t\\r\\n]+ -> skip;")

        g4 = "\n".join(g4_lines)
        notes.append(f"Generated {len(productions)} production(s) and {len(terminals)} terminal(s)")
        return True, g4, notes

    def validate_atoms(self, metta_atoms: str) -> tuple[bool, list[dict], list[str]]:
        """Validate MeTTa grammar spec atoms against structural rules.

        Runs the MeTTa validation rules (is-left-recursive, is-reachable, etc.)
        directly on the atom structure, not on .g4 text.
        """
        notes: list[str] = []
        issues: list[dict] = []

        try:
            self._metta.run(metta_atoms)
        except Exception as e:
            issues.append({
                "severity": pb2.GRAMMAR_ISSUE_SEVERITY_ERROR,
                "rule_name": "",
                "description": f"Failed to parse MeTTa atoms: {e}",
                "kind": pb2.GRAMMAR_ISSUE_KIND_SYNTAX_ERROR,
            })
            return False, issues, [str(e)]

        # Run the validate-grammar MeTTa function if a grammar is present
        try:
            validation_results = self._metta.run(
                "(match &self (= (validate-grammar $g) $result) $result)"
            )
            for group in validation_results:
                for atom in group:
                    atom_str = str(atom)
                    if atom_str.startswith("("):
                        # Parse issue strings like "LEFT_RECURSION:ruleName"
                        self._parse_validation_atom(atom_str, issues, notes)
        except Exception as e:
            notes.append(f"MeTTa validation query returned: {e}")

        # Also do structural validation by extracting and checking directly
        grammar_name, start_rule, productions, terminals = self._extract_grammar_structure(
            metta_atoms, notes
        )

        if grammar_name:
            # Check left recursion on extracted productions
            for name, alts in productions.items():
                for alt in alts:
                    tokens = alt.strip().split()
                    if tokens and tokens[0] == name:
                        issues.append({
                            "severity": pb2.GRAMMAR_ISSUE_SEVERITY_ERROR,
                            "rule_name": name,
                            "description": f"Direct left recursion in atom spec: {name} -> {name} ...",
                            "kind": pb2.GRAMMAR_ISSUE_KIND_LEFT_RECURSION,
                        })
                        break

            # Check for undefined references
            defined = set(productions.keys()) | set(terminals.keys())
            for name, alts in productions.items():
                for alt in alts:
                    for token in alt.strip().split():
                        cleaned = re.sub(r"[?*+()'\"]", "", token)
                        if (
                            cleaned
                            and cleaned[0].islower()
                            and cleaned not in defined
                            and cleaned not in ("true", "false", "null")
                        ):
                            issues.append({
                                "severity": pb2.GRAMMAR_ISSUE_SEVERITY_ERROR,
                                "rule_name": name,
                                "description": f"Atom spec references undefined rule '{cleaned}'",
                                "kind": pb2.GRAMMAR_ISSUE_KIND_MISSING_RULE,
                            })

        is_valid = not any(
            i["severity"] == pb2.GRAMMAR_ISSUE_SEVERITY_ERROR for i in issues
        )
        return is_valid, issues, notes

    def correct_atoms(
        self, metta_atoms: str, known_issues: list[dict]
    ) -> tuple[bool, str, list[str], list[dict]]:
        """Correct MeTTa grammar spec atoms by applying symbolic rewriting rules.

        Uses the fix-left-recursion and other correction rules from grammar_atoms.metta
        to transform the atom specification.
        """
        corrections: list[str] = []
        corrected = metta_atoms

        has_left_recursion = any(
            i.get("kind", 0) == pb2.GRAMMAR_ISSUE_KIND_LEFT_RECURSION
            for i in known_issues
        )

        if has_left_recursion:
            # Try to run MeTTa fix-left-recursion on the atoms
            try:
                fix_results = self._metta.run(
                    "(match &self (= (fix-left-recursion $prod) $result) $result)"
                )
                for group in fix_results:
                    for atom in group:
                        atom_str = str(atom)
                        if "MkProduction" in atom_str:
                            corrections.append(f"Applied fix-left-recursion: {atom_str[:100]}")
            except Exception as e:
                corrections.append(f"MeTTa correction attempted: {e}")

            # Also apply structural correction on the extracted atoms
            notes: list[str] = []
            grammar_name, start_rule, productions, terminals = self._extract_grammar_structure(
                metta_atoms, notes
            )

            if grammar_name:
                fixed_productions: dict[str, list[str]] = {}
                for rule_name in known_issues:
                    name = rule_name.get("rule_name", "")
                    if name and name in productions:
                        alts = productions[name]
                        recursive = []
                        non_recursive = []
                        for alt in alts:
                            tokens = alt.strip().split()
                            if tokens and tokens[0] == name:
                                recursive.append(" ".join(tokens[1:]))
                            else:
                                non_recursive.append(alt)

                        if recursive:
                            prime_name = f"{name}_prime"
                            new_alts = [f"{nr} {prime_name}" for nr in non_recursive] if non_recursive else [prime_name]
                            prime_alts = [f"{r} {prime_name}" for r in recursive] + ["/* epsilon */"]

                            fixed_productions[name] = new_alts
                            fixed_productions[prime_name] = prime_alts
                            corrections.append(f"Removed left recursion from '{name}' in atom spec")

                # Rebuild MeTTa atoms with corrections
                if fixed_productions:
                    corrected = self._rebuild_metta_atoms(
                        grammar_name, start_rule, productions, terminals, fixed_productions
                    )

        # Re-validate the corrected atoms
        is_valid, remaining_issues, _ = self.validate_atoms(corrected)
        return True, corrected, corrections, remaining_issues

    def _extract_grammar_structure(
        self, metta_atoms: str, notes: list[str]
    ) -> tuple[str, str, dict[str, list[str]], dict[str, str]]:
        """Extract grammar structure from MeTTa atom source text by parsing atom expressions."""
        grammar_name = ""
        start_rule = ""
        productions: dict[str, list[str]] = {}
        terminals: dict[str, str] = {}

        # Parse MkGrammar: (MkGrammar "name" "start" (Cons prod1 ...))
        grammar_match = re.search(
            r'\(MkGrammar\s+"([^"]+)"\s+"([^"]+)"',
            metta_atoms,
        )
        if grammar_match:
            grammar_name = grammar_match.group(1)
            start_rule = grammar_match.group(2)
            notes.append(f"Found grammar '{grammar_name}' with start rule '{start_rule}'")

        # Parse MkProduction: (MkProduction "name" (Cons (Cons "sym1" ...) ...))
        for prod_match in re.finditer(
            r'\(MkProduction\s+"([^"]+)"\s+\((.+?)\)\s*\)',
            metta_atoms,
        ):
            prod_name = prod_match.group(1)
            alts_raw = prod_match.group(2)

            # Extract alternatives from nested Cons lists
            alternatives = self._parse_cons_list_of_lists(alts_raw)
            if alternatives:
                productions[prod_name] = alternatives
            else:
                # Fallback: try simpler list format
                symbols = re.findall(r'"([^"]+)"', alts_raw)
                if symbols:
                    productions[prod_name] = [" ".join(symbols)]

        # Parse MkTerminal: (MkTerminal "NAME")
        for term_match in re.finditer(r'\(MkTerminal\s+"([^"]+)"\)', metta_atoms):
            term_name = term_match.group(1)
            terminals[term_name] = f"'{term_name}'"

        # Parse MkRegexTerminal: (MkRegexTerminal "NAME" "pattern")
        for regex_match in re.finditer(
            r'\(MkRegexTerminal\s+"([^"]+)"\s+"([^"]+)"\)',
            metta_atoms,
        ):
            term_name = regex_match.group(1)
            term_pattern = regex_match.group(2)
            terminals[term_name] = term_pattern

        return grammar_name, start_rule, productions, terminals

    def _parse_cons_list_of_lists(self, raw: str) -> list[str]:
        """Parse nested Cons-list structure into a list of space-separated alternatives."""
        alternatives: list[str] = []

        # Handle: Cons (Cons "a" (Cons "b" Nil)) (Cons (Cons "c" Nil) Nil)
        # Simplified: extract all quoted strings grouped by inner Cons blocks
        depth = 0
        current_alt: list[str] = []
        i = 0

        # Simpler approach: find all inner (Cons "..." ...) groups
        inner_lists = re.findall(r'\(Cons\s+((?:"[^"]*"\s*(?:\(Cons\s+)*)*)', raw)

        if not inner_lists:
            # Try flat list: "sym1" "sym2" | "sym3"
            all_symbols = re.findall(r'"([^"]+)"', raw)
            if all_symbols:
                return [" ".join(all_symbols)]
            return []

        # Group symbols by tracking Cons nesting for alternatives
        # Each top-level Cons entry is one alternative
        current: list[str] = []
        for item in re.findall(r'"([^"]+)"', raw):
            current.append(item)

        # Simple heuristic: split on "Nil)" boundaries to find alternatives
        parts = re.split(r'Nil\s*\)', raw)
        alternatives = []
        for part in parts:
            symbols = re.findall(r'"([^"]+)"', part)
            if symbols:
                alternatives.append(" ".join(symbols))

        return alternatives if alternatives else [" ".join(current)] if current else []

    @staticmethod
    def _parse_validation_atom(atom_str: str, issues: list[dict], notes: list[str]) -> None:
        """Parse a MeTTa validation result atom into issue dicts."""
        # Parse strings like "LEFT_RECURSION:ruleName" from validation results
        for item_str in re.findall(r'"([^"]+)"', atom_str):
            if ":" in item_str:
                kind_str, _, rule_name = item_str.partition(":")
                kind_map = {
                    "LEFT_RECURSION": pb2.GRAMMAR_ISSUE_KIND_LEFT_RECURSION,
                    "UNREACHABLE": pb2.GRAMMAR_ISSUE_KIND_UNREACHABLE_RULE,
                    "FIRST_CONFLICT": pb2.GRAMMAR_ISSUE_KIND_FIRST_SET_CONFLICT,
                    "MISSING_RULE": pb2.GRAMMAR_ISSUE_KIND_MISSING_RULE,
                }
                kind = kind_map.get(kind_str, pb2.GRAMMAR_ISSUE_KIND_UNSPECIFIED)
                severity = (
                    pb2.GRAMMAR_ISSUE_SEVERITY_ERROR
                    if kind_str in ("LEFT_RECURSION", "MISSING_RULE")
                    else pb2.GRAMMAR_ISSUE_SEVERITY_WARNING
                )
                issues.append({
                    "severity": severity,
                    "rule_name": rule_name,
                    "description": f"MeTTa validation: {kind_str} in '{rule_name}'",
                    "kind": kind,
                })

    @staticmethod
    def _rebuild_metta_atoms(
        grammar_name: str,
        start_rule: str,
        original_productions: dict[str, list[str]],
        terminals: dict[str, str],
        overrides: dict[str, list[str]],
    ) -> str:
        """Rebuild MeTTa atom source with corrected productions."""
        lines: list[str] = []

        # Build MkProduction atoms
        all_productions = dict(original_productions)
        all_productions.update(overrides)

        prod_atoms: list[str] = []
        for name, alts in all_productions.items():
            # Build Cons list of Cons lists for alternatives
            alt_atoms: list[str] = []
            for alt in alts:
                symbols = alt.split()
                sym_list = "Nil"
                for sym in reversed(symbols):
                    sym_list = f'(Cons "{sym}" {sym_list})'
                alt_atoms.append(sym_list)

            alts_list = "Nil"
            for alt_atom in reversed(alt_atoms):
                alts_list = f"(Cons {alt_atom} {alts_list})"

            prod_atoms.append(f'(MkProduction "{name}" {alts_list})')

        # Build productions list
        prods_list = "Nil"
        for prod in reversed(prod_atoms):
            prods_list = f"(Cons {prod} {prods_list})"

        # Build terminal atoms
        for name, pattern in terminals.items():
            if pattern.startswith("'"):
                lines.append(f'(MkTerminal "{name}")')
            else:
                lines.append(f'(MkRegexTerminal "{name}" "{pattern}")')

        # Build grammar atom
        lines.append(f'(MkGrammar "{grammar_name}" "{start_rule}" {prods_list})')

        return "\n".join(lines)

    @property
    def grammar_count(self) -> int:
        return len(self._proven_grammars)


class HyperonGrammarServicer(pb2_grpc.HyperonGrammarServiceServicer):
    """gRPC service implementation."""

    def __init__(self) -> None:
        self._space = GrammarAtomSpace()

    def ValidateGrammar(self, request, context):
        is_valid, issues = self._space.validate_grammar(request.grammar_g4)
        return pb2.ValidateGrammarResponse(
            is_valid=is_valid,
            issues=[
                pb2.GrammarIssue(
                    severity=i["severity"],
                    rule_name=i["rule_name"],
                    description=i["description"],
                    kind=i["kind"],
                )
                for i in issues
            ],
        )

    def CorrectGrammar(self, request, context):
        known = [
            {"kind": i.kind, "rule_name": i.rule_name, "description": i.description}
            for i in request.known_issues
        ]
        success, corrected, corrections, remaining = self._space.correct_grammar(
            request.grammar_g4, known
        )
        return pb2.CorrectGrammarResponse(
            success=success,
            corrected_grammar_g4=corrected,
            corrections_applied=corrections,
            remaining_issues=[
                pb2.GrammarIssue(
                    severity=i["severity"],
                    rule_name=i["rule_name"],
                    description=i["description"],
                    kind=i["kind"],
                )
                for i in remaining
            ],
        )

    def ComposeGrammars(self, request, context):
        success, composed, conflicts = self._space.compose_grammars(
            list(request.grammar_fragments), request.grammar_name
        )
        return pb2.ComposeGrammarsResponse(
            success=success,
            composed_grammar_g4=composed,
            conflicts_resolved=conflicts,
        )

    def RefineGrammar(self, request, context):
        failure = {
            "offending_token": request.failure.offending_token,
            "expected_tokens": request.failure.expected_tokens,
            "line": request.failure.line,
            "column": request.failure.column,
            "input_snippet": request.failure.input_snippet,
        }
        success, refined, explanation = self._space.refine_grammar(
            request.grammar_g4, failure
        )
        return pb2.RefineGrammarResponse(
            success=success,
            refined_grammar_g4=refined,
            refinement_explanation=explanation,
        )

    def StoreProvenGrammar(self, request, context):
        success, gid = self._space.store_proven_grammar(
            request.description,
            request.grammar_g4,
            list(request.sample_inputs),
        )
        return pb2.StoreProvenGrammarResponse(success=success, grammar_id=gid)

    def RetrieveGrammar(self, request, context):
        found, g4, gid, score = self._space.retrieve_grammar(request.description)
        return pb2.RetrieveGrammarResponse(
            found=found,
            grammar_g4=g4,
            grammar_id=gid,
            similarity_score=score,
        )

    # ----- Logic Transfer Object (LTO) RPC Handlers -----

    def AtomsToGrammar(self, request, context):
        success, g4, notes = self._space.atoms_to_g4(request.metta_atoms)
        return pb2.AtomsToGrammarResponse(
            success=success,
            grammar_g4=g4,
            notes=notes,
        )

    def ValidateAtoms(self, request, context):
        is_valid, issues, notes = self._space.validate_atoms(request.metta_atoms)
        return pb2.ValidateAtomsResponse(
            is_valid=is_valid,
            issues=[
                pb2.GrammarIssue(
                    severity=i["severity"],
                    rule_name=i["rule_name"],
                    description=i["description"],
                    kind=i["kind"],
                )
                for i in issues
            ],
            validation_notes=notes,
        )

    def CorrectAtoms(self, request, context):
        known = [
            {"kind": i.kind, "rule_name": i.rule_name, "description": i.description}
            for i in request.known_issues
        ]
        success, corrected, corrections, remaining = self._space.correct_atoms(
            request.metta_atoms, known
        )
        return pb2.CorrectAtomsResponse(
            success=success,
            corrected_metta_atoms=corrected,
            corrections_applied=corrections,
            remaining_issues=[
                pb2.GrammarIssue(
                    severity=i["severity"],
                    rule_name=i["rule_name"],
                    description=i["description"],
                    kind=i["kind"],
                )
                for i in remaining
            ],
        )

    def HealthCheck(self, request, context):
        return pb2.HealthCheckResponse(
            healthy=True,
            version=VERSION,
            grammars_stored=self._space.grammar_count,
        )


# =============================================================================
# Grammar Parsing Utilities
# =============================================================================


def _parse_g4_rules(grammar_g4: str) -> dict[str, list[str]]:
    """Parse ANTLR4 grammar text into a dict of rule_name -> alternatives."""
    rules: dict[str, list[str]] = {}
    # Match rule definitions: ruleName : alt1 | alt2 ;
    pattern = re.compile(
        r"^(\w+)\s*\n?\s*:\s*(.*?)\s*;",
        re.MULTILINE | re.DOTALL,
    )
    # Remove comments and grammar declaration
    cleaned = re.sub(r"//[^\n]*", "", grammar_g4)
    cleaned = re.sub(r"/\*.*?\*/", "", cleaned, flags=re.DOTALL)
    cleaned = re.sub(r"^\s*grammar\s+\w+\s*;", "", cleaned, flags=re.MULTILINE)

    for match in pattern.finditer(cleaned):
        name = match.group(1).strip()
        body = match.group(2).strip()
        alternatives = [alt.strip() for alt in body.split("|")]
        rules[name] = alternatives

    return rules


def _parse_g4_tokens(grammar_g4: str) -> dict[str, str]:
    """Parse lexer token rules from ANTLR4 grammar text."""
    tokens: dict[str, str] = {}
    pattern = re.compile(r"^([A-Z_]\w*)\s*:\s*(.*?)\s*;", re.MULTILINE)
    for match in pattern.finditer(grammar_g4):
        tokens[match.group(1)] = match.group(2)
    return tokens


def _find_start_rule(grammar_g4: str, rules: dict[str, list[str]]) -> str:
    """Find the start rule (first parser rule) in the grammar."""
    for name in rules:
        if name[0].islower():
            return name
    return ""


def _find_reachable(
    start: str, rules: dict[str, list[str]]
) -> set[str]:
    """Find all rules reachable from the start rule via BFS."""
    visited: set[str] = set()
    queue = [start]

    while queue:
        current = queue.pop(0)
        if current in visited:
            continue
        visited.add(current)

        for alt in rules.get(current, []):
            for token in alt.split():
                cleaned = re.sub(r"[?*+()'\"]", "", token)
                if cleaned in rules and cleaned not in visited:
                    queue.append(cleaned)

    return visited


def _fix_left_recursion(grammar_g4: str, rule_name: str) -> tuple[str, bool]:
    """Remove direct left recursion from a specific rule in the grammar text."""
    pattern = re.compile(
        rf"^({re.escape(rule_name)})\s*:\s*(.*?)\s*;",
        re.MULTILINE | re.DOTALL,
    )
    match = pattern.search(grammar_g4)
    if not match:
        return grammar_g4, False

    body = match.group(2)
    alternatives = [alt.strip() for alt in body.split("|")]

    recursive: list[str] = []
    non_recursive: list[str] = []

    for alt in alternatives:
        tokens = alt.split()
        if tokens and tokens[0] == rule_name:
            recursive.append(" ".join(tokens[1:]))
        else:
            non_recursive.append(alt)

    if not recursive:
        return grammar_g4, False

    prime_name = f"{rule_name}Prime"

    # Build new rule: A -> beta A'
    new_non_recursive = [f"{nr} {prime_name}" for nr in non_recursive] if non_recursive else [prime_name]
    new_rule = f"{rule_name}\n    : " + "\n    | ".join(new_non_recursive) + "\n    ;"

    # Build prime rule: A' -> alpha A' | /* epsilon */
    prime_alts = [f"{r} {prime_name}" for r in recursive] + ["/* epsilon */"]
    prime_rule = f"{prime_name}\n    : " + "\n    | ".join(prime_alts) + "\n    ;"

    # Replace original rule and insert prime rule after it
    replacement = f"{new_rule}\n\n{prime_rule}"
    corrected = grammar_g4[: match.start()] + replacement + grammar_g4[match.end() :]

    return corrected, True


def serve(port: int = 50051) -> None:
    """Start the gRPC server."""
    server = grpc.server(futures.ThreadPoolExecutor(max_workers=10))
    pb2_grpc.add_HyperonGrammarServiceServicer_to_server(
        HyperonGrammarServicer(), server
    )

    # Enable reflection for service discovery
    service_names = (
        pb2.DESCRIPTOR.services_by_name["HyperonGrammarService"].full_name,
        reflection.SERVICE_NAME,
    )
    reflection.enable_server_reflection(service_names, server)

    server.add_insecure_port(f"[::]:{port}")
    server.start()
    logger.info("Hyperon Grammar Service started on port %d", port)
    server.wait_for_termination()


if __name__ == "__main__":
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
    )
    port = int(os.environ.get("HYPERON_GRAMMAR_PORT", "50051"))
    serve(port)
