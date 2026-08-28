#!/usr/bin/env python3
"""Deterministic control plane for QF-AI-COLLAB-v5.

This module intentionally contains no provider SDK and performs no GitHub write.
It validates trusted inputs and returns state transitions for separately
permissioned workflows.
"""

from __future__ import annotations

import hashlib
import json
import math
import re
from dataclasses import dataclass
from datetime import date, datetime, timezone
from pathlib import Path
from typing import Any, Iterable


ROOT = Path(__file__).resolve().parents[2]
REGISTRY_DIR = ROOT / ".github/ai/registries"
SCHEMA_DIR = ROOT / ".github/ai/schemas"

DRAFT_2020_12 = "https://json-schema.org/draft/2020-12/schema"
APPROVED_SCHEMAS = {
    "work-order.schema.json": "work-order.schema.json@1",
    "manufacturing-result.schema.json": "manufacturing-result.schema.json@3",
    "review-request.schema.json": "review-request.schema.json@1",
    "technical-review.schema.json": "technical-review.schema.json@3",
    "finding-disposition-record.schema.json": "finding-disposition-record.schema.json@2",
}
SUPPORTED_SCHEMA_KEYWORDS = frozenset({
    "$schema", "$id", "title", "type", "additionalProperties", "required",
    "properties", "const", "enum", "items", "minItems", "uniqueItems",
    "minLength", "minimum", "maximum", "pattern", "format",
})
SUPPORTED_SCHEMA_TYPES = frozenset({
    "object", "array", "string", "integer", "boolean", "null",
})
SUPPORTED_SCHEMA_FORMATS = frozenset({"date", "date-time"})
RFC3339_DATE_TIME = re.compile(
    r"^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}"
    r"(?:\.[0-9]+)?(?:Z|[+-][0-9]{2}:[0-9]{2})$"
)

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
STOP_IMPLEMENTATIONS = {
    "STOP-001": "scripts.ai_controller.core.loop_transition",
    "STOP-002": "scripts.ai_controller.core.route_origin",
    "STOP-003": "scripts.ai_controller.core.validate_work_order",
    "STOP-004": "scripts.ai_controller.core.validate_work_order",
    "STOP-005": "scripts.ai_controller.core.validate_scope",
    "STOP-006": "scripts.ai_controller.core.validate_required_checks",
    "STOP-007": "scripts.ai_controller.core.validate_control_plane",
    "STOP-008": "scripts.ai_controller.core.validate_review_gate",
    "STOP-009": "scripts.ai_controller.core.validate_required_checks",
    "STOP-010": "scripts.ai_controller.core.validate_review_gate",
    "STOP-011": "scripts.ai_controller.core.validate_review_gate",
    "STOP-012": "scripts.ai_controller.core.validate_review_gate",
    "STOP-013": "scripts.ai_controller.core.validate_review_gate",
    "STOP-014": "scripts.ai_controller.core.validate_review_record",
    "STOP-015": "scripts.ai_controller.core.validate_review_gate",
    "STOP-016": "scripts.ai_controller.core.validate_disposition_record",
    "STOP-017": "scripts.ai_controller.core.validate_work_order",
    "STOP-018": "scripts.ai_controller.core.loop_transition",
    "STOP-019": "scripts.ai_controller.core.validate_review_gate",
    "STOP-020": "scripts.ai_controller.core.loop_transition",
    "STOP-021": "scripts.ai_controller.core.validate_required_checks",
    "STOP-022": "scripts.ai_controller.core.validate_work_order",
    "STOP-023": "scripts.ai_controller.core.validate_threat_baseline",
    "STOP-024": "scripts.ai_controller.core.validate_threat_baseline",
    "STOP-025": "scripts.ai_controller.core.validate_work_order",
    "STOP-026": "scripts.ai_controller.core.validate_work_order",
    "STOP-027": "scripts.ai_controller.core.validate_appointment",
    "STOP-028": "scripts.ai_controller.core.validate_appointment",
    "STOP-029": "scripts.ai_controller.core.appointment_applicability",
    "STOP-030": "scripts.ai_controller.core.validate_review_record",
    "STOP-031": "scripts.ai_controller.core.validate_disposition_record",
}


class ControllerError(ValueError):
    """A fail-closed controller decision."""


@dataclass(frozen=True)
class Decision:
    state: str
    accepted: bool
    reasons: tuple[str, ...]
    evidence: dict[str, Any]

    def as_dict(self) -> dict[str, Any]:
        evidence = dict(self.evidence)
        stop_ids = sorted({
            match
            for reason in self.reasons
            for match in re.findall(r"STOP-[0-9]{3}", reason)
        })
        if stop_ids:
            controller = dict(evidence.get("controller", {}))
            stop_conditions = dict(controller.get("stop_conditions", {}))
            for identifier in stop_ids:
                stop_conditions[identifier] = {
                    "result": "RED",
                    "implemented_by": STOP_IMPLEMENTATIONS.get(identifier, "unknown"),
                }
            controller["stop_conditions"] = stop_conditions
            evidence["controller"] = controller
        return {
            "protocol": PROTOCOL,
            "state": self.state,
            "accepted": self.accepted,
            "reasons": list(self.reasons),
            "evidence": evidence,
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


def _schema_location(path: tuple[str, ...]) -> str:
    return "$" + "".join(f".{part}" for part in path)


def _schema_types(schema: dict[str, Any]) -> tuple[str, ...]:
    value = schema.get("type")
    if value is None:
        return ()
    return (value,) if isinstance(value, str) else tuple(value)


def _schema_requires_type(
    schema: dict[str, Any], keyword: str, required_type: str, path: tuple[str, ...]
) -> None:
    if keyword in schema and required_type not in _schema_types(schema):
        raise ControllerError(
            f"schema {_schema_location(path)}: {keyword} requires type {required_type}"
        )


def validate_schema_definition(
    schema: dict[str, Any], *, expected_id: str | None = None
) -> None:
    """Validate the approved, dependency-free Draft 2020-12 keyword subset."""

    def walk(node: Any, path: tuple[str, ...]) -> None:
        location = _schema_location(path)
        if not isinstance(node, dict):
            raise ControllerError(f"schema {location}: definition must be an object")

        unknown = sorted(set(node) - SUPPORTED_SCHEMA_KEYWORDS)
        if unknown:
            raise ControllerError(f"schema {location}: unsupported keywords {unknown}")

        if path:
            metadata = sorted(set(node) & {"$schema", "$id", "title"})
            if metadata:
                raise ControllerError(
                    f"schema {location}: root-only keywords used {metadata}"
                )
        else:
            if node.get("$schema") != DRAFT_2020_12:
                raise ControllerError(
                    f"schema {location}: $schema must be Draft 2020-12"
                )
            if expected_id is not None and node.get("$id") != expected_id:
                raise ControllerError(
                    f"schema {location}: $id must be {expected_id}"
                )
            if "title" in node and not isinstance(node["title"], str):
                raise ControllerError(f"schema {location}: title must be a string")

        declared_type = node.get("type")
        if declared_type is not None:
            if isinstance(declared_type, str):
                declared_types = (declared_type,)
            elif (
                isinstance(declared_type, list)
                and declared_type
                and all(isinstance(item, str) for item in declared_type)
            ):
                declared_types = tuple(declared_type)
            else:
                raise ControllerError(
                    f"schema {location}: type must be a string or non-empty string list"
                )
            unsupported_types = sorted(set(declared_types) - SUPPORTED_SCHEMA_TYPES)
            if unsupported_types or len(declared_types) != len(set(declared_types)):
                raise ControllerError(
                    f"schema {location}: unsupported or duplicate types {list(declared_types)}"
                )

        properties = node.get("properties")
        if properties is not None:
            if not isinstance(properties, dict) or not all(
                isinstance(name, str) for name in properties
            ):
                raise ControllerError(f"schema {location}: properties must be an object")
            for name, child in properties.items():
                walk(child, path + ("properties", name))

        required = node.get("required")
        if required is not None:
            if (
                not isinstance(required, list)
                or not all(isinstance(item, str) for item in required)
                or len(required) != len(set(required))
            ):
                raise ControllerError(
                    f"schema {location}: required must contain unique strings"
                )
            if properties is not None and not set(required) <= set(properties):
                raise ControllerError(
                    f"schema {location}: required names must exist in properties"
                )

        if "additionalProperties" in node and not isinstance(
            node["additionalProperties"], bool
        ):
            raise ControllerError(
                f"schema {location}: only boolean additionalProperties is supported"
            )

        enum = node.get("enum")
        if enum is not None:
            if not isinstance(enum, list) or not enum:
                raise ControllerError(f"schema {location}: enum must be a non-empty list")
            canonical_values = [canonical_json(item) for item in enum]
            if len(canonical_values) != len(set(canonical_values)):
                raise ControllerError(f"schema {location}: enum values must be unique")

        items = node.get("items")
        if items is not None:
            walk(items, path + ("items",))

        for keyword in ("minLength", "minItems"):
            if keyword in node and (
                not isinstance(node[keyword], int)
                or isinstance(node[keyword], bool)
                or node[keyword] < 0
            ):
                raise ControllerError(
                    f"schema {location}: {keyword} must be a non-negative integer"
                )

        for keyword in ("minimum", "maximum"):
            if keyword in node and (
                not isinstance(node[keyword], (int, float))
                or isinstance(node[keyword], bool)
                or not math.isfinite(node[keyword])
            ):
                raise ControllerError(f"schema {location}: {keyword} must be finite")
        if "minimum" in node and "maximum" in node and node["minimum"] > node["maximum"]:
            raise ControllerError(f"schema {location}: minimum exceeds maximum")

        if "uniqueItems" in node and not isinstance(node["uniqueItems"], bool):
            raise ControllerError(f"schema {location}: uniqueItems must be boolean")

        if "pattern" in node:
            if not isinstance(node["pattern"], str):
                raise ControllerError(f"schema {location}: pattern must be a string")
            try:
                re.compile(node["pattern"])
            except re.error as exc:
                raise ControllerError(
                    f"schema {location}: pattern is invalid: {exc}"
                ) from exc

        if "format" in node and node["format"] not in SUPPORTED_SCHEMA_FORMATS:
            raise ControllerError(
                f"schema {location}: unsupported format {node['format']!r}"
            )

        for keyword in ("properties", "required", "additionalProperties"):
            _schema_requires_type(node, keyword, "object", path)
        for keyword in ("items", "minItems", "uniqueItems"):
            _schema_requires_type(node, keyword, "array", path)
        for keyword in ("minLength", "pattern", "format"):
            _schema_requires_type(node, keyword, "string", path)
        for keyword in ("minimum", "maximum"):
            _schema_requires_type(node, keyword, "integer", path)

    walk(schema, ())


def load_approved_schema(name: str) -> dict[str, Any]:
    expected_id = APPROVED_SCHEMAS.get(name)
    if expected_id is None:
        raise ControllerError(f"unapproved schema: {name}")
    schema = load_json_yaml(SCHEMA_DIR / name)
    if not isinstance(schema, dict):
        raise ControllerError(f"schema {name}: root must be an object")
    validate_schema_definition(schema, expected_id=expected_id)
    return schema


def _json_type_matches(value: Any, expected: str) -> bool:
    if expected == "object":
        return isinstance(value, dict)
    if expected == "array":
        return isinstance(value, list)
    if expected == "string":
        return isinstance(value, str)
    if expected == "integer":
        return (
            isinstance(value, int) and not isinstance(value, bool)
        ) or (
            isinstance(value, float) and math.isfinite(value) and value.is_integer()
        )
    if expected == "boolean":
        return isinstance(value, bool)
    if expected == "null":
        return value is None
    return False


def _instance_location(path: tuple[str | int, ...]) -> str:
    location = "$"
    for part in path:
        location += f"[{part}]" if isinstance(part, int) else f".{part}"
    return location


def _format_is_valid(value: str, name: str) -> bool:
    try:
        if name == "date":
            return re.fullmatch(r"[0-9]{4}-[0-9]{2}-[0-9]{2}", value) is not None and bool(
                date.fromisoformat(value)
            )
        if name == "date-time":
            return RFC3339_DATE_TIME.fullmatch(value) is not None and bool(
                datetime.fromisoformat(value.replace("Z", "+00:00"))
            )
    except ValueError:
        return False
    return False


def _validate_instance(
    value: Any,
    schema: dict[str, Any],
    path: tuple[str | int, ...],
    errors: list[str],
) -> None:
    location = _instance_location(path)
    declared_types = _schema_types(schema)
    if declared_types and not any(
        _json_type_matches(value, expected) for expected in declared_types
    ):
        errors.append(f"{location}:type")
        return

    if "const" in schema and canonical_json(value) != canonical_json(schema["const"]):
        errors.append(f"{location}:const")
    if "enum" in schema and canonical_json(value) not in {
        canonical_json(item) for item in schema["enum"]
    }:
        errors.append(f"{location}:enum")

    if isinstance(value, dict):
        properties = schema.get("properties", {})
        for name in schema.get("required", []):
            if name not in value:
                errors.append(f"{location}.{name}:required")
        if schema.get("additionalProperties") is False:
            for name in sorted(set(value) - set(properties)):
                errors.append(f"{location}.{name}:additionalProperties")
        for name, child_schema in properties.items():
            if name in value:
                _validate_instance(value[name], child_schema, path + (name,), errors)

    if isinstance(value, list):
        if len(value) < schema.get("minItems", 0):
            errors.append(f"{location}:minItems")
        if schema.get("uniqueItems") is True:
            canonical_items = [canonical_json(item) for item in value]
            if len(canonical_items) != len(set(canonical_items)):
                errors.append(f"{location}:uniqueItems")
        if "items" in schema:
            for index, item in enumerate(value):
                _validate_instance(item, schema["items"], path + (index,), errors)

    if isinstance(value, str):
        if len(value) < schema.get("minLength", 0):
            errors.append(f"{location}:minLength")
        if "pattern" in schema and re.search(schema["pattern"], value) is None:
            errors.append(f"{location}:pattern")
        if "format" in schema and not _format_is_valid(value, schema["format"]):
            errors.append(f"{location}:format")

    if _json_type_matches(value, "integer"):
        if "minimum" in schema and value < schema["minimum"]:
            errors.append(f"{location}:minimum")
        if "maximum" in schema and value > schema["maximum"]:
            errors.append(f"{location}:maximum")


def schema_validation_errors(document: Any, schema_name: str) -> tuple[str, ...]:
    """Return deterministic errors for a document and one approved Schema."""

    schema = load_approved_schema(schema_name)
    errors: list[str] = []
    _validate_instance(document, schema, (), errors)
    return tuple(errors)


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
        "manufacturing_denylist": validate_registry("manufacturing-denylist.yml"),
    }
    deny_document = load_registry("manufacturing-denylist.yml")
    _, deny_entries = _entries(deny_document)
    declared_prefixes = {
        str(item.get("path")) for item in deny_entries if item.get("kind") == "prefix"
    }
    declared_files = {
        str(item.get("path")) for item in deny_entries if item.get("kind") == "file"
    }
    if declared_prefixes != set(CONTROL_PLANE_DENYLIST) or declared_files != CONTROL_PLANE_DENY_FILES:
        raise ControllerError("manufacturing denylist registry/implementation drift")
    stop_document = load_registry("stop-conditions.yml")
    _, stop_entries = _entries(stop_document)
    declared_stop_implementations = {
        str(item.get("id")): str(item.get("implemented_by")) for item in stop_entries
    }
    if declared_stop_implementations != STOP_IMPLEMENTATIONS:
        raise ControllerError("STOP registry/implementation drift")
    result["stop_conditions"]["alignment"] = check_registry_execution(
        "stop-conditions.yml",
        STOP_IMPLEMENTATIONS,
        {
            identifier: {
                "result": "GREEN",
                "evidence": implementation,
            }
            for identifier, implementation in STOP_IMPLEMENTATIONS.items()
        },
    )
    expected_schemas = set(APPROVED_SCHEMAS)
    missing = [name for name in sorted(expected_schemas) if not (SCHEMA_DIR / name).is_file()]
    if missing:
        raise ControllerError(f"missing schemas: {missing}")
    for name in expected_schemas:
        load_approved_schema(name)
    result["schemas"] = sorted(expected_schemas)
    result["schema_ids"] = [APPROVED_SCHEMAS[name] for name in sorted(expected_schemas)]
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
    if not isinstance(path, str) or not path or "\x00" in path or "\\" in path:
        return True
    if path.startswith("/") or re.match(r"^[A-Za-z]:/", path):
        return True
    normalized = path[2:] if path.startswith("./") else path
    if not normalized:
        return True
    segments = normalized.split("/")
    if any(segment in {"", ".", ".."} for segment in segments):
        return True
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
    base_is_ancestor: bool = True,
    now: datetime | None = None,
    dedup_state: str = "first",
    visibility: str = "public",
) -> Decision:
    now = now or datetime.now(timezone.utc)
    checks: dict[str, dict[str, Any]] = {}
    reasons: list[str] = []

    work_order_schema_errors = schema_validation_errors(
        work_order, "work-order.schema.json"
    )
    work_order = work_order if isinstance(work_order, dict) else {}
    metadata_value = work_order.get("metadata", {})
    spec_value = work_order.get("spec", {})
    approval_value = work_order.get("approval", {})
    metadata = metadata_value if isinstance(metadata_value, dict) else {}
    spec = spec_value if isinstance(spec_value, dict) else {}
    approval = approval_value if isinstance(approval_value, dict) else {}
    expected_hash = content_hash(spec)
    expected_execution = f"{metadata.get('id')}:{metadata.get('version')}:{expected_hash}"
    budget_value = approval.get("budget", {})
    budget = budget_value if isinstance(budget_value, dict) else {}
    risk = spec.get("risk_class")
    organizer = metadata.get("organizer")
    allowlist = set(organizer_logins())
    organizer_is_allowed = isinstance(organizer, str) and organizer in allowlist
    risk_is_known = isinstance(risk, str) and risk in {
        "normal", "sensitive", "governance",
    }
    required_tests_value = spec.get("required_tests", [])
    required_tests = (
        required_tests_value
        if isinstance(required_tests_value, list)
        and all(isinstance(item, str) for item in required_tests_value)
        else []
    )
    workflow_sha = workflow_sha or default_branch_sha
    values = {
        "WO-PRE-001": (SHA40.fullmatch(default_branch_sha or "") is not None, default_branch_sha),
        "WO-PRE-002": (actor in allowlist, actor),
        "WO-PRE-003": (organizer_is_allowed, organizer),
        "WO-PRE-004": (approval.get("organizer_decision") == "APPROVED", approval.get("organizer_decision")),
        "WO-PRE-005": (
            not work_order_schema_errors,
            {"id": metadata.get("id"), "schema_errors": list(work_order_schema_errors)},
        ),
        "WO-PRE-006": (approval.get("execution_state") == "READY", approval.get("execution_state")),
        "WO-PRE-007": (approval.get("execution_id") == expected_execution, expected_execution),
        "WO-PRE-008": (dedup_state in {"first", "processed"}, dedup_state),
        "WO-PRE-009": (approval.get("work_order_hash") == expected_hash, expected_hash),
        "WO-PRE-010": (False, approval.get("expires_at")),
        "WO-PRE-011": (
            SHA40.fullmatch(str(approval.get("base_sha", ""))) is not None
            and SHA256.fullmatch(str(approval.get("work_order_hash", ""))) is not None
            and base_is_ancestor,
            {
                "base_sha": approval.get("base_sha"),
                "work_order_hash": approval.get("work_order_hash"),
                "base_is_default_ancestor": base_is_ancestor,
            },
        ),
        "WO-PRE-012": (
            set(required_tests) == set(required_check_names()),
            required_tests,
        ),
        "WO-PRE-013": (False, approval.get("second_human_decision")),
        "WO-PRE-014": (False, budget),
        "WO-PRE-015": (visibility == "public", visibility),
        "WO-PRE-016": (bool(spec.get("scope")) and bool(spec.get("prohibited_paths")), spec.get("scope")),
        "WO-PRE-017": (risk_is_known, risk),
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
            isinstance(risk, str)
            and risk in {"sensitive", "governance"}
            and bool(second_reviewer)
            and second_reviewer != organizer
            and second_decision == "APPROVED"
            and approval.get("second_human_approved_sha") == default_branch_sha
        ),
        {"reviewer": second_reviewer, "decision": second_decision},
    )
    values["WO-PRE-014"] = (
        isinstance(budget.get("max_iterations"), int)
        and not isinstance(budget.get("max_iterations"), bool)
        and 0 <= budget["max_iterations"] <= 3
        and all(
            isinstance(budget.get(field), int)
            and not isinstance(budget.get(field), bool)
            and budget[field] > 0
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

    schema_errors = schema_validation_errors(
        result, "manufacturing-result.schema.json"
    )
    result = result if isinstance(result, dict) else {}
    work_order = work_order if isinstance(work_order, dict) else {}
    metadata_value = work_order.get("metadata", {})
    spec_value = work_order.get("spec", {})
    metadata = metadata_value if isinstance(metadata_value, dict) else {}
    spec = spec_value if isinstance(spec_value, dict) else {}
    paths = sorted(set(changed_paths))
    required_fields = {
        "protocol", "schema_version", "role", "work_order_id", "work_order_ref",
        "work_order_hash", "base_sha", "patch_sha256", "scope_changed",
        "tests_requested", "known_limits", "iteration",
    }
    reasons: list[str] = []
    if schema_errors:
        reasons.append("manufacturing-result-json-schema")
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
    scope_changed_value = result.get("scope_changed", [])
    scope_changed = (
        scope_changed_value
        if isinstance(scope_changed_value, list)
        and all(isinstance(item, str) for item in scope_changed_value)
        else []
    )
    tests_requested_value = result.get("tests_requested", [])
    tests_requested = (
        tests_requested_value
        if isinstance(tests_requested_value, list)
        and all(isinstance(item, str) for item in tests_requested_value)
        else []
    )
    required_tests_value = spec.get("required_tests", [])
    required_tests = (
        required_tests_value
        if isinstance(required_tests_value, list)
        and all(isinstance(item, str) for item in required_tests_value)
        else []
    )
    allowed_scope_value = spec.get("scope", [])
    allowed_scope = (
        allowed_scope_value
        if isinstance(allowed_scope_value, list)
        and all(isinstance(item, str) for item in allowed_scope_value)
        else []
    )
    prohibited_paths_value = spec.get("prohibited_paths", [])
    prohibited_paths = (
        prohibited_paths_value
        if isinstance(prohibited_paths_value, list)
        and all(isinstance(item, str) for item in prohibited_paths_value)
        else []
    )
    if sorted(set(scope_changed)) != paths:
        reasons.append("manufacturing-result-scope")
    if set(tests_requested) != set(required_tests):
        reasons.append("manufacturing-result-tests")
    if not isinstance(result.get("known_limits"), list):
        reasons.append("manufacturing-result-known-limits")
    iteration = result.get("iteration")
    if (
        not isinstance(iteration, int)
        or isinstance(iteration, bool)
        or not 1 <= iteration <= 4
    ):
        reasons.append("manufacturing-result-iteration")
    scope = validate_scope(paths, allowed_scope, prohibited_paths)
    if not scope.accepted:
        reasons.extend(scope.reasons)
    return Decision(
        "qf:manufactured" if not reasons else "qf:stopped",
        not reasons,
        tuple(sorted(set(reasons))),
        {
            "changed_paths": paths,
            "patch_sha256": patch_sha256,
            "schema_errors": list(schema_errors),
        },
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
    request_schema_errors = schema_validation_errors(
        request, "review-request.schema.json"
    )
    review_schema_errors = schema_validation_errors(
        review if review is not None else {}, "technical-review.schema.json"
    )
    request = request if isinstance(request, dict) else {}
    review = review if isinstance(review, dict) else {}
    coverage_value = review.get("coverage", {})
    coverage = coverage_value if isinstance(coverage_value, dict) else {}
    findings_value = review.get("findings", [])
    findings = (
        [item for item in findings_value if isinstance(item, dict)]
        if isinstance(findings_value, list)
        else []
    )
    eligible_value = request.get("eligible_finding_ids", [])
    eligible_finding_ids = (
        eligible_value
        if isinstance(eligible_value, list)
        and all(isinstance(item, str) for item in eligible_value)
        else []
    )
    fix_candidates_value = request.get("fix_candidates", [])
    fix_candidates = (
        [item for item in fix_candidates_value if isinstance(item, dict)]
        if isinstance(fix_candidates_value, list)
        else []
    )
    mode = request.get("expected_review_mode")
    verified_eligible = set(eligible_finding_ids)
    fix_ids = {
        item["finding_id"]
        for item in fix_candidates
        if isinstance(item.get("finding_id"), str)
    }
    reviewed_sha = review.get("reviewed_commit_sha")

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
        not request_schema_errors
        and request.get("protocol") == PROTOCOL
        and request.get("schema_version") == "review-request.schema.json@1"
        and isinstance(request.get("pr_number"), int) and request.get("pr_number") >= 1
        and SHA40.fullmatch(str(request.get("head_sha", ""))) is not None,
        {
            "schema_version": request.get("schema_version"),
            "schema_errors": list(request_schema_errors),
        },
    )
    values["REV-GATE-008"] = (
        not review_schema_errors
        and review.get("protocol") == PROTOCOL
        and review.get("schema_version") == "technical-review.schema.json@3"
        and review.get("role") == "claude_reviewer"
        and review.get("work_order_id") == request.get("work_order_id")
        and SHA40.fullmatch(str(review.get("default_branch_sha", ""))) is not None,
        {
            "schema_version": review.get("schema_version"),
            "role": review.get("role"),
            "work_order_id": review.get("work_order_id"),
            "schema_errors": list(review_schema_errors),
        },
    )
    decision = review.get("decision")
    decision_consistent = (
        (decision == "PASS" and not findings and review.get("blocking") is False)
        or (decision == "PASS_WITH_FINDINGS" and bool(findings) and review.get("blocking") is False)
        or (decision == "FAIL" and review.get("blocking") is True)
    )
    values["REV-GATE-009"] = (
        isinstance(decision, str)
        and decision in {"PASS", "PASS_WITH_FINDINGS", "FAIL"}
        and decision_consistent,
        {"decision": decision, "blocking": review.get("blocking"), "findings": len(findings)},
    )
    initial_shape = (
        request.get("prior_review_artifact_sha256") is None
        and eligible_finding_ids == []
        and fix_candidates == []
    )
    reverify_shape = (
        SHA256.fullmatch(str(request.get("prior_review_artifact_sha256", ""))) is not None
        and bool(eligible_finding_ids)
        and bool(fix_candidates)
    )
    values["REV-GATE-010"] = (
        isinstance(mode, str)
        and mode in {"INITIAL", "REVERIFY"}
        and review.get("review_mode") == mode
        and ((mode == "INITIAL" and initial_shape) or (mode == "REVERIFY" and reverify_shape)),
        mode,
    )
    expected_request_hash = content_hash(request)
    values["REV-GATE-011"] = (review.get("review_request_sha256") == expected_request_hash, expected_request_hash)
    finding_ids = [item.get("id") for item in findings]
    finding_ids_are_strings = all(isinstance(item, str) for item in finding_ids)
    required_finding_fields = {
        "id", "severity", "verification_status", "disposition", "path", "evidence",
        "risk", "required_change", "residual_risk",
    }
    values["REV-GATE-012"] = (
        finding_ids_are_strings
        and len(finding_ids) == len(set(finding_ids))
        and all(
            FINDING_ID.fullmatch(str(item.get("id", "")))
            and isinstance(item.get("severity"), str)
            and item.get("severity") in {"P0", "P1", "P2", "P3"}
            and required_finding_fields == set(item)
            for item in findings
        ),
        finding_ids,
    )
    allowed_status = {"OPEN"} if mode == "INITIAL" else {"OPEN", "VERIFIED"}
    values["REV-GATE-013"] = (
        all(
            isinstance(item.get("verification_status"), str)
            and item.get("verification_status") in allowed_status
            for item in findings
        ),
        allowed_status,
    )
    verified_ids = {
        item["id"]
        for item in findings
        if item.get("verification_status") == "VERIFIED"
        and isinstance(item.get("id"), str)
    }
    candidate_by_id = {
        item["finding_id"]: item
        for item in fix_candidates
        if isinstance(item.get("finding_id"), str)
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
    p0p1 = [
        item for item in findings
        if isinstance(item.get("severity"), str)
        and item.get("severity") in {"P0", "P1"}
    ]
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
    blocking = any(
        isinstance(item.get("severity"), str)
        and item.get("severity") in {"P0", "P1"}
        for item in findings
    )
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
    schema_errors = schema_validation_errors(
        record, "finding-disposition-record.schema.json"
    )
    record = record if isinstance(record, dict) else {}
    review_record = review_record if isinstance(review_record, dict) else None
    required_record_fields = {
        "protocol", "schema_version", "role", "decided_by", "decided_at",
        "review_artifact_sha256", "reviewed_commit_sha", "supersedes_record_sha256",
        "decisions",
    }
    if schema_errors:
        reasons.append("disposition-record-json-schema")
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
    review_findings_value = (review_record or {}).get("findings", [])
    review_findings = (
        [item for item in review_findings_value if isinstance(item, dict)]
        if isinstance(review_findings_value, list)
        else []
    )
    review_ids = {
        item["id"] for item in review_findings if isinstance(item.get("id"), str)
    }
    decisions_value = record.get("decisions", [])
    decisions = (
        [item for item in decisions_value if isinstance(item, dict)]
        if isinstance(decisions_value, list)
        else []
    )
    for decision in decisions:
        finding_id = decision.get("finding_id")
        if not isinstance(finding_id, str) or finding_id not in review_ids:
            reasons.append("unknown-finding")
        disposition = decision.get("disposition")
        if disposition == "DEFERRED":
            deferral_value = decision.get("deferral", {})
            deferral = deferral_value if isinstance(deferral_value, dict) else {}
            try:
                due = date.fromisoformat(str(deferral.get("due")))
            except (TypeError, ValueError):
                due = date.min
            if not deferral.get("owner") or not deferral.get("reason") or due < now:
                reasons.append("deferral-incomplete")
        elif disposition == "REJECTED_WITH_REASON" and not decision.get("reason"):
            reasons.append("rejection-reason-missing")
        elif disposition == "POLICY_DECISION_REQUIRED" and (
            not decision.get("policy_owner") or not decision.get("decision_due")
        ):
            reasons.append("policy-decision-incomplete")
        elif not isinstance(disposition, str) or disposition not in {
            "ACCEPTED_PLAN", "REJECTED_WITH_REASON", "DEFERRED", "POLICY_DECISION_REQUIRED", "CLOSED"
        }:
            reasons.append("disposition-enum")
    supersedes = record.get("supersedes_record_sha256")
    if supersedes is not None and (
        not isinstance(supersedes, str)
        or supersedes not in set(existing_record_hashes)
    ):
        reasons.append("supersedes-invalid")
    digest = content_hash(record)
    path = f"docs/evidence/automation/dispositions/{review_hash}/{digest}.json"
    return Decision(
        "qf:disposition-valid" if not reasons else "qf:stopped",
        not reasons,
        tuple(sorted(set(reasons))),
        {"path": path, "sha256": digest, "schema_errors": list(schema_errors)},
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
    pr_author: str | None = None,
    nominee_in_organizer_allowlist: bool = False,
) -> Decision:
    review = latest_review or {}
    reasons: list[str] = []
    if organizer not in organizer_logins() or nominee == organizer:
        reasons.append("independence")
    if pr_author is not None and pr_author != organizer:
        reasons.append("appointment-author")
    if nominee_in_organizer_allowlist:
        reasons.append("nominee-organizer")
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


def validate_threat_baseline(
    baseline: dict[str, Any], *, expected_phase: str | None = None
) -> Decision:
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
    if expected_phase is not None and phase != expected_phase:
        reasons.append("STOP-023:phase-mismatch")
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
        {
            "baseline_hash": content_hash(baseline),
            "baseline_phase": phase,
            "expected_phase": expected_phase,
        },
    )
