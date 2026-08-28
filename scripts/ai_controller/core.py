#!/usr/bin/env python3
"""Deterministic control plane for QF-AI-COLLAB-v5.

This module intentionally contains no provider SDK and performs no GitHub write.
It validates trusted inputs and returns state transitions for separately
permissioned workflows.
"""

from __future__ import annotations

import hashlib
import json
import re
from dataclasses import dataclass
from datetime import date, datetime, timezone
from pathlib import Path
from typing import Any, Iterable


ROOT = Path(__file__).resolve().parents[2]
REGISTRY_DIR = ROOT / ".github/ai/registries"
SCHEMA_DIR = ROOT / ".github/ai/schemas"

PROTOCOL = "QF-AI-COLLAB-v5"
SHA40 = re.compile(r"^[0-9a-f]{40}$")
SHA256 = re.compile(r"^[0-9a-f]{64}$")
FINDING_ID = re.compile(r"^AUTO-IMPL-P[0-3]-[0-9]{3}$")

CONTROL_PLANE_DENYLIST = (
    ".github/ai/",
    ".github/workflows/",
    "docs/governance/",
    "docs/evidence/automation/reviews/",
    "docs/evidence/automation/dispositions/",
    "scripts/ai_controller/",
    "tests/automation/",
)
CONTROL_PLANE_DENY_FILES = {
    "AGENTS.md", "CLAUDE.md", "REVIEW.md",
    "scripts/qf-ai-controller.py", "scripts/test-github-autodrive-controller.sh",
}

GATE_IDS = tuple(f"REV-GATE-{number:03d}" for number in range(1, 21))
PRECONDITION_IDS = tuple(f"WO-PRE-{number:03d}" for number in range(1, 19))
STOP_IDS = tuple(f"STOP-{number:03d}" for number in range(1, 32))


class ControllerError(ValueError):
    """A fail-closed controller decision."""


@dataclass(frozen=True)
class Decision:
    state: str
    accepted: bool
    reasons: tuple[str, ...]
    evidence: dict[str, Any]

    def as_dict(self) -> dict[str, Any]:
        return {
            "protocol": PROTOCOL,
            "state": self.state,
            "accepted": self.accepted,
            "reasons": list(self.reasons),
            "evidence": self.evidence,
        }


def canonical_json(value: Any) -> str:
    return json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":"))


def sha256_text(value: str) -> str:
    return hashlib.sha256(value.encode("utf-8")).hexdigest()


def content_hash(value: Any) -> str:
    return sha256_text(canonical_json(value))


def load_json_yaml(path: Path) -> Any:
    """Load the JSON-compatible subset of YAML used by the control plane."""

    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise ControllerError(f"unreadable control-plane document {path}: {exc}") from exc


def _entries(document: dict[str, Any]) -> tuple[str, list[dict[str, Any]]]:
    keys = [key for key, value in document.items() if isinstance(value, list)]
    if len(keys) != 1:
        raise ControllerError("registry must contain exactly one entry list")
    return keys[0], document[keys[0]]


def load_registry(name: str) -> dict[str, Any]:
    return load_json_yaml(REGISTRY_DIR / name)


def registry_ids(name: str) -> tuple[str, ...]:
    document = load_registry(name)
    _, entries = _entries(document)
    return tuple(str(item.get("id", "")) for item in entries)


def validate_registry(name: str, implemented_ids: Iterable[str] | None = None) -> dict[str, Any]:
    document = load_registry(name)
    if document.get("schema_version") != 1:
        raise ControllerError(f"{name}: schema_version must be 1")
    _, entries = _entries(document)
    required_fields = {"id", "name", "required", "owner", "implemented_by", "evidence_field"}
    ids: list[str] = []
    for index, entry in enumerate(entries):
        missing = required_fields - set(entry)
        if missing:
            raise ControllerError(f"{name}[{index}] missing {sorted(missing)}")
        if entry["required"] is not True:
            raise ControllerError(f"{name}:{entry['id']} required may not be weakened")
        if not all(str(entry[field]).strip() for field in required_fields - {"required"}):
            raise ControllerError(f"{name}:{entry.get('id')} contains an empty field")
        ids.append(str(entry["id"]))
    if len(ids) != len(set(ids)):
        raise ControllerError(f"{name}: duplicate IDs")
    if implemented_ids is not None and set(ids) != set(implemented_ids):
        missing = sorted(set(ids) - set(implemented_ids))
        unknown = sorted(set(implemented_ids) - set(ids))
        raise ControllerError(f"{name}: registry/implementation drift missing={missing} unknown={unknown}")
    return {"ids": ids, "sha256": sha256_text((REGISTRY_DIR / name).read_text(encoding="utf-8"))}


def validate_control_plane() -> dict[str, Any]:
    result = {
        "gate_checks": validate_registry("gate-checks.yml", GATE_IDS),
        "work_order_preconditions": validate_registry(
            "work-order-preconditions.yml", PRECONDITION_IDS
        ),
        "stop_conditions": validate_registry("stop-conditions.yml", STOP_IDS),
        "required_checks": validate_registry("required-checks.yml"),
        "finding_ids": validate_registry("finding-ids.yml"),
        "organizer_allowlist": validate_registry("organizer-allowlist.yml"),
    }
    expected_schemas = {
        "work-order.schema.json",
        "manufacturing-result.schema.json",
        "review-request.schema.json",
        "technical-review.schema.json",
        "finding-disposition-record.schema.json",
    }
    missing = [name for name in sorted(expected_schemas) if not (SCHEMA_DIR / name).is_file()]
    if missing:
        raise ControllerError(f"missing schemas: {missing}")
    for name in expected_schemas:
        load_json_yaml(SCHEMA_DIR / name)
    result["schemas"] = sorted(expected_schemas)
    return result


def check_registry_execution(
    registry_name: str,
    implemented_ids: Iterable[str],
    executed: dict[str, dict[str, Any]],
) -> dict[str, Any]:
    expected = set(registry_ids(registry_name))
    implemented = set(implemented_ids)
    executed_ids = set(executed)
    duplicate_free = len(executed) == len(executed_ids)
    missing_evidence = sorted(
        item for item in expected if item in executed and not executed[item].get("evidence")
    )
    accepted = (
        expected == implemented == executed_ids
        and duplicate_free
        and not missing_evidence
        and all(executed[item].get("result") in {"GREEN", "RED"} for item in expected)
    )
    return {
        "accepted": accepted,
        "expected": sorted(expected),
        "implemented": sorted(implemented),
        "executed": sorted(executed_ids),
        "missing_evidence": missing_evidence,
    }


def required_check_names() -> tuple[str, ...]:
    document = load_registry("required-checks.yml")
    _, entries = _entries(document)
    return tuple(str(item["check_name"]) for item in entries)


def organizer_logins() -> tuple[str, ...]:
    document = load_registry("organizer-allowlist.yml")
    _, entries = _entries(document)
    return tuple(str(item["github_login"]) for item in entries)


def path_is_denied(path: str) -> bool:
    normalized = path.lstrip("./")
    return normalized in CONTROL_PLANE_DENY_FILES or normalized.startswith(CONTROL_PLANE_DENYLIST)


def validate_scope(changed_paths: Iterable[str], allowed: Iterable[str], prohibited: Iterable[str]) -> Decision:
    allowed_prefixes = tuple(str(item).rstrip("*") for item in allowed)
    prohibited_prefixes = tuple(str(item).rstrip("*") for item in prohibited)
    errors: list[str] = []
    paths = tuple(sorted(set(changed_paths)))
    for path in paths:
        if path_is_denied(path):
            errors.append(f"control-plane-denied:{path}")
        if prohibited_prefixes and any(path.startswith(item) for item in prohibited_prefixes):
            errors.append(f"work-order-prohibited:{path}")
        if allowed_prefixes and not any(path.startswith(item) for item in allowed_prefixes):
            errors.append(f"scope-exceeded:{path}")
    return Decision(
        state="qf:stopped" if errors else "qf:manufacturing",
        accepted=not errors,
        reasons=tuple(errors),
        evidence={"changed_paths": list(paths)},
    )


def _parse_time(value: str) -> datetime:
    parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    if parsed.tzinfo is None:
        parsed = parsed.replace(tzinfo=timezone.utc)
    return parsed.astimezone(timezone.utc)


def validate_work_order(
    work_order: dict[str, Any],
    *,
    actor: str,
    default_branch_sha: str,
    workflow_sha: str | None = None,
    now: datetime | None = None,
    dedup_state: str = "first",
    visibility: str = "public",
) -> Decision:
    now = now or datetime.now(timezone.utc)
    checks: dict[str, dict[str, Any]] = {}
    reasons: list[str] = []

    metadata = work_order.get("metadata", {})
    spec = work_order.get("spec", {})
    approval = work_order.get("approval", {})
    expected_hash = content_hash(spec)
    expected_execution = f"{metadata.get('id')}:{metadata.get('version')}:{expected_hash}"
    budget = approval.get("budget", {})
    risk = spec.get("risk_class")
    organizer = metadata.get("organizer")
    allowlist = set(organizer_logins())
    metadata_fields = {"id", "version", "source_issue", "organizer", "created_at"}
    spec_fields = {
        "objective", "source_question", "scope", "out_of_scope", "acceptance_criteria",
        "required_tests", "prohibited_paths", "risk_class", "evidence_required",
        "stop_conditions", "rollback_plan",
    }
    approval_required = {
        "base_sha", "work_order_hash", "expires_at", "budget", "organizer_decision",
        "execution_state", "execution_id", "second_human_reviewer", "second_human_decision",
    }
    approval_allowed = approval_required | {"second_human_approved_sha"}
    work_order_shape = (
        set(work_order) == {"metadata", "spec", "approval"}
        and set(metadata) == metadata_fields
        and set(spec) == spec_fields
        and approval_required <= set(approval) <= approval_allowed
        and re.fullmatch(r"WO-[0-9]{4,}", str(metadata.get("id", ""))) is not None
        and isinstance(metadata.get("version"), int) and metadata.get("version") >= 1
        and (metadata.get("source_issue") is None or isinstance(metadata.get("source_issue"), int))
        and all(isinstance(spec.get(field), list) for field in (
            "scope", "out_of_scope", "acceptance_criteria", "required_tests",
            "prohibited_paths", "evidence_required", "stop_conditions",
        ))
        and all(isinstance(spec.get(field), str) and spec.get(field) for field in (
            "objective", "source_question", "rollback_plan",
        ))
    )

    workflow_sha = workflow_sha or default_branch_sha
    values = {
        "WO-PRE-001": (SHA40.fullmatch(default_branch_sha or "") is not None, default_branch_sha),
        "WO-PRE-002": (actor in allowlist, actor),
        "WO-PRE-003": (organizer in allowlist, organizer),
        "WO-PRE-004": (approval.get("organizer_decision") == "APPROVED", approval.get("organizer_decision")),
        "WO-PRE-005": (work_order_shape, metadata.get("id")),
        "WO-PRE-006": (approval.get("execution_state") == "READY", approval.get("execution_state")),
        "WO-PRE-007": (approval.get("execution_id") == expected_execution, expected_execution),
        "WO-PRE-008": (dedup_state in {"first", "processed"}, dedup_state),
        "WO-PRE-009": (approval.get("work_order_hash") == expected_hash, expected_hash),
        "WO-PRE-010": (False, approval.get("expires_at")),
        "WO-PRE-011": (
            SHA40.fullmatch(str(approval.get("base_sha", ""))) is not None
            and SHA256.fullmatch(str(approval.get("work_order_hash", ""))) is not None,
            {"base_sha": approval.get("base_sha"), "work_order_hash": approval.get("work_order_hash")},
        ),
        "WO-PRE-012": (set(spec.get("required_tests", [])) == set(required_check_names()), spec.get("required_tests", [])),
        "WO-PRE-013": (False, approval.get("second_human_decision")),
        "WO-PRE-014": (False, budget),
        "WO-PRE-015": (visibility == "public", visibility),
        "WO-PRE-016": (bool(spec.get("scope")) and bool(spec.get("prohibited_paths")), spec.get("scope")),
        "WO-PRE-017": (risk in {"normal", "sensitive", "governance"}, risk),
        "WO-PRE-018": (
            SHA40.fullmatch(workflow_sha or "") is not None and workflow_sha == default_branch_sha,
            {"workflow_sha": workflow_sha, "default_branch_sha": default_branch_sha},
        ),
    }
    try:
        values["WO-PRE-010"] = (_parse_time(str(approval.get("expires_at"))) > now, approval.get("expires_at"))
    except (TypeError, ValueError):
        values["WO-PRE-010"] = (False, approval.get("expires_at"))

    second_reviewer = approval.get("second_human_reviewer")
    second_decision = approval.get("second_human_decision")
    values["WO-PRE-013"] = (
        (risk == "normal" and second_decision == "NOT_REQUIRED")
        or (
            risk in {"sensitive", "governance"}
            and bool(second_reviewer)
            and second_reviewer != organizer
            and second_decision == "APPROVED"
            and approval.get("second_human_approved_sha") == default_branch_sha
        ),
        {"reviewer": second_reviewer, "decision": second_decision},
    )
    values["WO-PRE-014"] = (
        budget.get("max_iterations") in {0, 1, 2, 3}
        and all(
            isinstance(budget.get(field), int) and budget[field] > 0
            for field in (
                "max_wall_minutes",
                "max_openai_tokens",
                "max_anthropic_tokens",
                "max_actions_minutes",
            )
        ),
        budget,
    )

    for identifier in PRECONDITION_IDS:
        passed, evidence = values[identifier]
        checks[identifier] = {"result": "GREEN" if passed else "RED", "evidence": evidence}
        if not passed:
            reasons.append(identifier)

    alignment = check_registry_execution(
        "work-order-preconditions.yml", PRECONDITION_IDS, checks
    )
    if not alignment["accepted"]:
        reasons.append("STOP-007")
    if dedup_state == "processed" and not reasons:
        return Decision(
            state="qf:no-op",
            accepted=True,
            reasons=("already-processed",),
            evidence={"preconditions": checks, "alignment": alignment},
        )
    return Decision(
        state="qf:manufacturing" if not reasons else "qf:stopped",
        accepted=not reasons,
        reasons=tuple(reasons),
        evidence={"preconditions": checks, "alignment": alignment},
    )


def route_origin(
    *,
    repository: str,
    head_repository: str | None,
    repository_id: int,
    head_repository_id: int | None,
    branch: str,
) -> Decision:
    if head_repository is None or head_repository != repository:
        return Decision(
            state="qf:no-op",
            accepted=True,
            reasons=("external-or-indeterminate-origin",),
            evidence={"artifact_read": False, "secret_read": False, "write_attempted": False},
        )
    group = f"qf-ai-{head_repository_id or repository_id}-{branch}"
    return Decision(
        state="qf:route",
        accepted=True,
        reasons=(),
        evidence={"concurrency_group": group},
    )


def validate_required_checks(
    registry_checks: Iterable[str],
    work_order_checks: Iterable[str],
    actual: dict[str, dict[str, str]],
    expected_sha: str,
) -> Decision:
    registry_set = set(registry_checks)
    work_order_set = set(work_order_checks)
    actual_set = set(actual)
    reasons: list[str] = []
    if not (registry_set == work_order_set == actual_set):
        reasons.append("AUTO-T09a")
    for name, result in actual.items():
        if result.get("conclusion") != "success" or result.get("head_sha") != expected_sha:
            reasons.append(f"AUTO-T09b:{name}")
    return Decision(
        state="qf:ci-green" if not reasons else "qf:ci-red",
        accepted=not reasons,
        reasons=tuple(reasons),
        evidence={
            "registry": sorted(registry_set),
            "work_order": sorted(work_order_set),
            "actual": sorted(actual_set),
            "head_sha": expected_sha,
        },
    )


def validate_patch_identity(manufactured: str, verified: str, published: str) -> Decision:
    values = (manufactured, verified, published)
    valid = all(SHA256.fullmatch(value or "") is not None for value in values)
    identical = len(set(values)) == 1
    return Decision(
        state="qf:patch-verified" if valid and identical else "qf:stopped",
        accepted=valid and identical,
        reasons=() if valid and identical else ("patch-hash-mismatch",),
        evidence={"manufactured": manufactured, "verified": verified, "published": published},
    )


def validate_manufacturing_result(
    result: dict[str, Any],
    work_order: dict[str, Any],
    *,
    patch_sha256: str,
    base_sha: str,
    changed_paths: Iterable[str],
) -> Decision:
    """Validate the manufacturer envelope without trusting its self-report."""

    metadata = work_order.get("metadata", {})
    spec = work_order.get("spec", {})
    paths = sorted(set(changed_paths))
    required_fields = {
        "protocol", "schema_version", "role", "work_order_id", "work_order_ref",
        "work_order_hash", "base_sha", "patch_sha256", "scope_changed",
        "tests_requested", "known_limits", "iteration",
    }
    reasons: list[str] = []
    if set(result) != required_fields:
        reasons.append("manufacturing-result-shape")
    if result.get("protocol") != PROTOCOL:
        reasons.append("manufacturing-result-protocol")
    if result.get("schema_version") != "manufacturing-result.schema.json@3":
        reasons.append("manufacturing-result-schema")
    if result.get("role") != "codex_manufacturer":
        reasons.append("manufacturing-result-role")
    if result.get("work_order_id") != metadata.get("id"):
        reasons.append("manufacturing-result-work-order")
    if result.get("work_order_hash") != content_hash(spec):
        reasons.append("manufacturing-result-work-order-hash")
    if result.get("base_sha") != base_sha or SHA40.fullmatch(base_sha or "") is None:
        reasons.append("manufacturing-result-base")
    if result.get("patch_sha256") != patch_sha256 or SHA256.fullmatch(patch_sha256 or "") is None:
        reasons.append("manufacturing-result-patch")
    if sorted(set(result.get("scope_changed", []))) != paths:
        reasons.append("manufacturing-result-scope")
    if set(result.get("tests_requested", [])) != set(spec.get("required_tests", [])):
        reasons.append("manufacturing-result-tests")
    if not isinstance(result.get("known_limits"), list):
        reasons.append("manufacturing-result-known-limits")
    if result.get("iteration") not in {1, 2, 3, 4}:
        reasons.append("manufacturing-result-iteration")
    scope = validate_scope(paths, spec.get("scope", []), spec.get("prohibited_paths", []))
    if not scope.accepted:
        reasons.extend(scope.reasons)
    return Decision(
        "qf:manufactured" if not reasons else "qf:stopped",
        not reasons,
        tuple(sorted(set(reasons))),
        {"changed_paths": paths, "patch_sha256": patch_sha256},
    )


def validate_review_gate(
    request: dict[str, Any],
    review: dict[str, Any] | None,
    *,
    current_head_sha: str,
    actual_tree_sha: str,
    required_checks_green: bool,
    claude_exit_code: int = 0,
    base_drift_allowed: bool = True,
    expected_default_branch_sha: str | None = None,
) -> Decision:
    values: dict[str, tuple[bool, Any]] = {}
    review = review or {}
    coverage = review.get("coverage", {})
    findings = review.get("findings", []) if isinstance(review.get("findings", []), list) else []
    mode = request.get("expected_review_mode")
    verified_eligible = set(request.get("eligible_finding_ids", []))
    fix_ids = {item.get("finding_id") for item in request.get("fix_candidates", [])}
    reviewed_sha = review.get("reviewed_commit_sha")
    request_fields = {
        "protocol", "schema_version", "request_id", "work_order_id", "pr_number",
        "expected_review_mode", "head_sha", "tree_sha", "prior_review_artifact_sha256",
        "eligible_finding_ids", "fix_candidates",
    }
    review_fields = {
        "protocol", "schema_version", "role", "review_mode", "review_request_sha256",
        "work_order_id", "reviewed_commit_sha", "reviewed_tree_sha", "default_branch_sha",
        "decision", "coverage", "findings", "notes", "blocking", "unverified",
    }

    values["REV-GATE-001"] = (claude_exit_code == 0, claude_exit_code)
    values["REV-GATE-002"] = (bool(review), bool(review))
    values["REV-GATE-003"] = (reviewed_sha == current_head_sha, {"reviewed": reviewed_sha, "head": current_head_sha})
    values["REV-GATE-004"] = (
        request.get("tree_sha") == review.get("reviewed_tree_sha") == actual_tree_sha,
        {
            "request": request.get("tree_sha"),
            "reviewed": review.get("reviewed_tree_sha"),
            "actual": actual_tree_sha,
        },
    )
    values["REV-GATE-005"] = (required_checks_green, required_checks_green)
    values["REV-GATE-006"] = (
        all(key in coverage for key in ("files_read", "checks_confirmed", "review_areas"))
        and "unverified" in review,
        coverage,
    )
    values["REV-GATE-007"] = (
        request.get("protocol") == PROTOCOL
        and request.get("schema_version") == "review-request.schema.json@1"
        and set(request) == request_fields
        and isinstance(request.get("pr_number"), int) and request.get("pr_number") >= 1
        and SHA40.fullmatch(str(request.get("head_sha", ""))) is not None,
        request.get("schema_version"),
    )
    values["REV-GATE-008"] = (
        review.get("protocol") == PROTOCOL
        and review.get("schema_version") == "technical-review.schema.json@3"
        and set(review) == review_fields
        and review.get("role") == "claude_reviewer"
        and review.get("work_order_id") == request.get("work_order_id")
        and SHA40.fullmatch(str(review.get("default_branch_sha", ""))) is not None,
        {
            "schema_version": review.get("schema_version"),
            "role": review.get("role"),
            "work_order_id": review.get("work_order_id"),
        },
    )
    decision = review.get("decision")
    decision_consistent = (
        (decision == "PASS" and not findings and review.get("blocking") is False)
        or (decision == "PASS_WITH_FINDINGS" and bool(findings) and review.get("blocking") is False)
        or (decision == "FAIL" and review.get("blocking") is True)
    )
    values["REV-GATE-009"] = (
        decision in {"PASS", "PASS_WITH_FINDINGS", "FAIL"} and decision_consistent,
        {"decision": decision, "blocking": review.get("blocking"), "findings": len(findings)},
    )
    initial_shape = (
        request.get("prior_review_artifact_sha256") is None
        and request.get("eligible_finding_ids") == []
        and request.get("fix_candidates") == []
    )
    reverify_shape = (
        SHA256.fullmatch(str(request.get("prior_review_artifact_sha256", ""))) is not None
        and bool(request.get("eligible_finding_ids"))
        and bool(request.get("fix_candidates"))
    )
    values["REV-GATE-010"] = (
        mode in {"INITIAL", "REVERIFY"}
        and review.get("review_mode") == mode
        and ((mode == "INITIAL" and initial_shape) or (mode == "REVERIFY" and reverify_shape)),
        mode,
    )
    expected_request_hash = content_hash(request)
    values["REV-GATE-011"] = (review.get("review_request_sha256") == expected_request_hash, expected_request_hash)
    finding_ids = [item.get("id") for item in findings]
    required_finding_fields = {
        "id", "severity", "verification_status", "disposition", "path", "evidence",
        "risk", "required_change", "residual_risk",
    }
    values["REV-GATE-012"] = (
        len(finding_ids) == len(set(finding_ids))
        and all(
            FINDING_ID.fullmatch(str(item.get("id", "")))
            and item.get("severity") in {"P0", "P1", "P2", "P3"}
            and required_finding_fields == set(item)
            for item in findings
        ),
        finding_ids,
    )
    allowed_status = {"OPEN"} if mode == "INITIAL" else {"OPEN", "VERIFIED"}
    values["REV-GATE-013"] = (all(item.get("verification_status") in allowed_status for item in findings), allowed_status)
    verified_ids = {item.get("id") for item in findings if item.get("verification_status") == "VERIFIED"}
    candidate_by_id = {
        item.get("finding_id"): item for item in request.get("fix_candidates", [])
        if isinstance(item, dict)
    }
    candidate_identity = all(
        item.get("candidate_sha") == current_head_sha
        and SHA256.fullmatch(str(item.get("record_sha256", ""))) is not None
        for item in candidate_by_id.values()
    )
    values["REV-GATE-014"] = (
        verified_ids <= verified_eligible
        and verified_ids <= fix_ids
        and candidate_identity,
        {"verified": sorted(verified_ids), "candidates": sorted(fix_ids)},
    )
    values["REV-GATE-015"] = (all(item.get("verification_status") != "CLOSED" for item in findings), "CLOSED forbidden")
    values["REV-GATE-016"] = (all(item.get("disposition") == "UNDECIDED" for item in findings), "UNDECIDED only")
    p0p1 = [item for item in findings if item.get("severity") in {"P0", "P1"}]
    values["REV-GATE-017"] = (not p0p1 or review.get("blocking") is True, [item.get("id") for item in p0p1])
    values["REV-GATE-018"] = (request.get("head_sha") == current_head_sha == reviewed_sha, current_head_sha)
    default_branch_matches = (
        expected_default_branch_sha is None
        or review.get("default_branch_sha") == expected_default_branch_sha
    )
    values["REV-GATE-019"] = (
        base_drift_allowed and default_branch_matches,
        {
            "allowed": base_drift_allowed,
            "reviewed_default": review.get("default_branch_sha"),
            "expected_default": expected_default_branch_sha,
        },
    )
    values["REV-GATE-020"] = (True, "registry execution evaluated below")

    executed = {
        identifier: {"result": "GREEN" if passed else "RED", "evidence": evidence}
        for identifier, (passed, evidence) in values.items()
    }
    alignment = check_registry_execution("gate-checks.yml", GATE_IDS, executed)
    if not alignment["accepted"]:
        executed["REV-GATE-020"] = {"result": "RED", "evidence": alignment}
    reasons = tuple(identifier for identifier in GATE_IDS if executed[identifier]["result"] != "GREEN")
    blocking = any(item.get("severity") in {"P0", "P1"} for item in findings)
    if reasons:
        state = "qf:stopped"
    elif blocking or findings:
        state = "qf:changes-requested"
    else:
        state = "qf:review-green"
    return Decision(
        state=state,
        accepted=not reasons,
        reasons=reasons,
        evidence={"gate_checks": executed, "alignment": alignment},
    )


def loop_transition(
    review: dict[str, Any],
    *,
    phase: str,
    iteration: int,
    max_iterations: int,
    previous_patch_hash: str | None = None,
    patch_hash: str | None = None,
    budget_exceeded: bool = False,
) -> Decision:
    findings = review.get("findings", [])
    reasons: list[str] = []
    if budget_exceeded or iteration > max_iterations:
        reasons.append("STOP-020")
    if patch_hash and previous_patch_hash and patch_hash == previous_patch_hash:
        reasons.append("STOP-018")
    if any(item.get("severity") in {"P0", "P1"} for item in findings):
        reasons.append("STOP-001")
    if reasons:
        return Decision("qf:stopped", False, tuple(reasons), {"iteration": iteration})
    if not findings:
        return Decision("qf:organizer-acceptance", True, (), {"iteration": iteration})
    phase_limits = {"A": 0, "B": 1, "C": 3}
    permitted = phase_limits.get(phase, 0)
    if phase == "A" or iteration >= min(permitted, max_iterations):
        return Decision("qf:changes-requested", True, ("human-return",), {"iteration": iteration})
    return Decision("qf:manufacturing", True, ("bounded-retry",), {"iteration": iteration + 1})


def review_record_path(review: dict[str, Any]) -> tuple[str, str]:
    sha = str(review.get("reviewed_commit_sha", ""))
    if SHA40.fullmatch(sha) is None:
        raise ControllerError("reviewed_commit_sha is invalid")
    digest = content_hash(review)
    return f"docs/evidence/automation/reviews/{sha}/{digest}.json", digest


def validate_review_record(path: str, record: dict[str, Any], *, exists_on_default: bool) -> Decision:
    expected_path, digest = review_record_path(record)
    reasons: list[str] = []
    if path != expected_path:
        reasons.append("review-record-path-or-hash")
    if not exists_on_default:
        reasons.append("review-record-not-durable")
    return Decision(
        "qf:review-record-valid" if not reasons else "qf:stopped",
        not reasons,
        tuple(reasons),
        {"path": expected_path, "sha256": digest},
    )


def validate_disposition_record(
    record: dict[str, Any],
    *,
    review_record: dict[str, Any] | None,
    existing_record_hashes: Iterable[str] = (),
    now: date | None = None,
) -> Decision:
    now = now or date.today()
    reasons: list[str] = []
    required_record_fields = {
        "protocol", "schema_version", "role", "decided_by", "decided_at",
        "review_artifact_sha256", "reviewed_commit_sha", "supersedes_record_sha256",
        "decisions",
    }
    if set(record) != required_record_fields:
        reasons.append("disposition-record-shape")
    if record.get("protocol") != PROTOCOL or record.get("schema_version") != "finding-disposition-record.schema.json@2" or record.get("role") != "organizer":
        reasons.append("disposition-record-schema")
    actor = record.get("decided_by")
    if actor not in organizer_logins():
        reasons.append("disposition-actor")
    review_hash = record.get("review_artifact_sha256")
    if not review_record or content_hash(review_record) != review_hash:
        reasons.append("review-artifact-reference")
    review_ids = {item.get("id") for item in (review_record or {}).get("findings", [])}
    for decision in record.get("decisions", []):
        if decision.get("finding_id") not in review_ids:
            reasons.append("unknown-finding")
        disposition = decision.get("disposition")
        if disposition == "DEFERRED":
            deferral = decision.get("deferral", {})
            try:
                due = date.fromisoformat(str(deferral.get("due")))
            except ValueError:
                due = date.min
            if not deferral.get("owner") or not deferral.get("reason") or due < now:
                reasons.append("deferral-incomplete")
        elif disposition == "REJECTED_WITH_REASON" and not decision.get("reason"):
            reasons.append("rejection-reason-missing")
        elif disposition == "POLICY_DECISION_REQUIRED" and (
            not decision.get("policy_owner") or not decision.get("decision_due")
        ):
            reasons.append("policy-decision-incomplete")
        elif disposition not in {
            "ACCEPTED_PLAN", "REJECTED_WITH_REASON", "DEFERRED", "POLICY_DECISION_REQUIRED", "CLOSED"
        }:
            reasons.append("disposition-enum")
    supersedes = record.get("supersedes_record_sha256")
    if supersedes is not None and supersedes not in set(existing_record_hashes):
        reasons.append("supersedes-invalid")
    digest = content_hash(record)
    path = f"docs/evidence/automation/dispositions/{review_hash}/{digest}.json"
    return Decision(
        "qf:disposition-valid" if not reasons else "qf:stopped",
        not reasons,
        tuple(sorted(set(reasons))),
        {"path": path, "sha256": digest},
    )


def appointment_applicability(
    changed_files: list[dict[str, Any]], *, api_success: bool, pagination_complete: bool
) -> Decision:
    paths: list[str] = []
    for item in changed_files:
        paths.append(str(item.get("filename", "")))
        if item.get("previous_filename"):
            paths.append(str(item["previous_filename"]))
    evidence = {
        "paths": paths,
        "api_count": len(changed_files),
        "pagination_complete": pagination_complete,
    }
    if not api_success or not pagination_complete or not changed_files or any(not path for path in paths):
        return Decision("qf:stopped", False, ("applicability-indeterminate",), evidence)
    target = "docs/governance/role-appointments/INDEPENDENT-AUTOMATION-RELEASE-REVIEWER.yml"
    applicable = target in paths
    return Decision(
        "applicable" if applicable else "not_applicable",
        True,
        (),
        evidence,
    )


def validate_appointment(
    *,
    organizer: str,
    nominee: str,
    head_sha: str,
    latest_review: dict[str, Any] | None,
    nominee_has_write: bool,
    only_appointment_path_changed: bool,
) -> Decision:
    review = latest_review or {}
    reasons: list[str] = []
    if organizer not in organizer_logins() or nominee == organizer:
        reasons.append("independence")
    if nominee_has_write:
        reasons.append("nominee-write")
    if not only_appointment_path_changed:
        reasons.append("mixed-change")
    if review.get("user") != nominee or review.get("state") != "APPROVED":
        reasons.append("review-state")
    if review.get("commit_id") != head_sha:
        reasons.append("stale-approval")
    return Decision(
        "qf:role-appointment-green" if not reasons else "qf:stopped",
        not reasons,
        tuple(reasons),
        {"review": review, "head_sha": head_sha},
    )


def public_output_is_clean(value: Any, canaries: Iterable[str]) -> bool:
    serialized = canonical_json(value)
    return all(canary not in serialized for canary in canaries)


def validate_threat_baseline(baseline: dict[str, Any]) -> Decision:
    reasons: list[str] = []
    if baseline.get("repository", {}).get("visibility") != "public":
        reasons.append("STOP-024")
    app = baseline.get("github_app", {})
    permissions = app.get("permissions", {})
    if app.get("branch_protection_bypass") is not False:
        reasons.append("app-bypass")
    if permissions.get("workflows") != "none" or permissions.get("actions") != "none":
        reasons.append("app-privilege")
    phase = baseline.get("automation", {}).get("phase")
    appointment = baseline.get("role_appointment", {})
    if phase == "BOOTSTRAP_DISABLED":
        if appointment.get("status") != "VACANT":
            reasons.append("unexpected-bootstrap-role")
    elif phase == "A":
        def contains_unmeasured(value: Any) -> bool:
            if value == "NOT_MEASURED":
                return True
            if isinstance(value, dict):
                return any(contains_unmeasured(item) for item in value.values())
            if isinstance(value, list):
                return any(contains_unmeasured(item) for item in value)
            return False

        if appointment.get("status") != "APPOINTED":
            reasons.append("phase-enabled-before-appointment")
        if contains_unmeasured(baseline):
            reasons.append("STOP-023")
        if app.get("installed") is not True or not app.get("repository_scope"):
            reasons.append("app-not-measured-or-installed")
        if not baseline.get("automation", {}).get("default_branch_workflows"):
            reasons.append("default-workflows-unmeasured")
    else:
        reasons.append("unsupported-phase")
    return Decision(
        ("qf:bootstrap-safe" if phase == "BOOTSTRAP_DISABLED" else "qf:phase-a-baseline")
        if not reasons else "qf:stopped",
        not reasons,
        tuple(reasons),
        {"baseline_hash": content_hash(baseline)},
    )
