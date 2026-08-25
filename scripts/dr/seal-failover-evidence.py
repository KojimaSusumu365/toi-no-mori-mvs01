#!/usr/bin/env python3
"""Validate, canonicalize, and SHA-256 seal native DR failover evidence."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import sys
import tempfile
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


MEASUREMENT_SCOPE = "native-local-dual-cluster-role-drill"
EXPECTED_KEYS = {
    "schemaVersion",
    "incidentId",
    "isSimulated",
    "measurementScope",
    "physicalRegionFailover",
    "topology",
    "approvals",
    "timeline",
    "safety",
    "schemaContract",
    "metrics",
}
TIMELINE_KEYS = (
    "disasterDeclaredAtUtc",
    "sourceWriteIsolatedAtUtc",
    "recoveryRestoreStartedAtUtc",
    "recoveryRestoreCompletedAtUtc",
    "recoveryAcceptedAtUtc",
    "routeSwitchedAtUtc",
)
DENIED_KEY_PARTS = (
    "password",
    "secret",
    "token",
    "cookie",
    "credential",
    "connectionstring",
    "privatekey",
)
SAFE_IDENTIFIER = re.compile(r"^[A-Za-z0-9._:@/-]{1,200}$")


class EvidenceValidationError(ValueError):
    pass


def require(condition: bool, message: str) -> None:
    if not condition:
        raise EvidenceValidationError(message)


def parse_utc(value: object, field: str) -> datetime:
    require(isinstance(value, str) and value.endswith("Z"), f"{field} must be UTC with Z suffix")
    try:
        parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    except ValueError as error:
        raise EvidenceValidationError(f"{field} is not a valid UTC timestamp") from error
    require(parsed.tzinfo is not None and parsed.utcoffset().total_seconds() == 0, f"{field} must be UTC")
    return parsed


def reject_sensitive_keys(value: Any, path: str = "$") -> None:
    if isinstance(value, dict):
        for key, child in value.items():
            folded = str(key).replace("_", "").lower()
            require(not any(part in folded for part in DENIED_KEY_PARTS), f"sensitive key is forbidden at {path}.{key}")
            reject_sensitive_keys(child, f"{path}.{key}")
    elif isinstance(value, list):
        for index, child in enumerate(value):
            reject_sensitive_keys(child, f"{path}[{index}]")
    elif isinstance(value, str):
        require("Host=" not in value and "Password=" not in value, f"connection material is forbidden at {path}")


def validate(candidate: dict[str, Any]) -> dict[str, bool]:
    require(set(candidate) == EXPECTED_KEYS, "candidate fields do not match the failover evidence schema")
    require(candidate["schemaVersion"] == "1.0", "unsupported evidence schemaVersion")
    require(isinstance(candidate["incidentId"], str) and SAFE_IDENTIFIER.fullmatch(candidate["incidentId"]) is not None, "incidentId is invalid")
    require(candidate["isSimulated"] is False, "native gate rejects simulated evidence")
    require(candidate["measurementScope"] == MEASUREMENT_SCOPE, "measurementScope is invalid")
    require(candidate["physicalRegionFailover"] is False, "local gate must not claim physical region failover")

    topology = candidate["topology"]
    require(isinstance(topology, dict), "topology must be an object")
    require(topology.get("sourceRole") == "ishikari-primary", "sourceRole must be ishikari-primary")
    require(topology.get("recoveryRole") == "tokyo-recovery", "recoveryRole must be tokyo-recovery")
    require(topology.get("routeSwitchMode") == "local-logical-gate", "routeSwitchMode is invalid")

    approvals = candidate["approvals"]
    require(isinstance(approvals, list) and len(approvals) == 2, "exactly two approvals are required")
    by_role = {approval.get("role"): approval for approval in approvals if isinstance(approval, dict)}
    require(set(by_role) == {"IncidentCommander", "RecoveryLead"}, "required approval roles are missing")
    subjects = []
    for role, decision in (
        ("IncidentCommander", "approve-recovery"),
        ("RecoveryLead", "approve-route-switch"),
    ):
        approval = by_role[role]
        subject = approval.get("subjectId")
        require(isinstance(subject, str) and SAFE_IDENTIFIER.fullmatch(subject) is not None, f"{role} subjectId is invalid")
        require(approval.get("decision") == decision, f"{role} decision is invalid")
        parse_utc(approval.get("approvedAtUtc"), f"approvals.{role}.approvedAtUtc")
        subjects.append(subject)
    require(len(set(subjects)) == 2, "approval subjects must be distinct")

    timeline = candidate["timeline"]
    require(isinstance(timeline, dict), "timeline must be an object")
    snapshot = parse_utc(timeline.get("snapshotStartedAtUtc"), "timeline.snapshotStartedAtUtc")
    ordered = [parse_utc(timeline.get(key), f"timeline.{key}") for key in TIMELINE_KEYS]
    require(snapshot <= ordered[0], "snapshot must not start after disaster declaration")
    require(all(left <= right for left, right in zip(ordered, ordered[1:])), "failover timeline is out of order")
    route_switched = ordered[-1]
    for approval in approvals:
        require(parse_utc(approval["approvedAtUtc"], "approval.approvedAtUtc") <= route_switched, "route switched before approval")

    safety = candidate["safety"]
    require(isinstance(safety, dict), "safety must be an object")
    require(safety.get("sourceWriteIsolated") is True, "source write path is not isolated")
    require(safety.get("recoveryWriteEnabled") is True, "recovery write path is not enabled")
    require(safety.get("simultaneousWritePrimaries") is False, "simultaneous write primaries are forbidden")
    require(safety.get("physicalGslbChanged") is False, "local gate must not report physical GSLB mutation")

    schema = candidate["schemaContract"]
    require(isinstance(schema, dict), "schemaContract must be an object")
    latest = schema.get("latestMigrationVersion")
    require(isinstance(latest, str) and latest.endswith(".005_stage6r7_append_only.sql"), "latest migration 005 was not restored")
    require(schema.get("migrationCount") == 5, "exactly five migrations must be restored")
    require(schema.get("fkPublishedRevisionSameQuestion") is True, "published revision composite FK was not restored")
    require(schema.get("platformSecurityEvents") is True, "platform security table was not restored")

    metrics = candidate["metrics"]
    require(isinstance(metrics, dict), "metrics must be an object")
    rpo = metrics.get("rpoSeconds")
    rto = metrics.get("rtoSeconds")
    require(isinstance(rpo, int) and 0 <= rpo <= 3600, "RPO exceeds the provisional target")
    require(isinstance(rto, int) and 0 <= rto <= 14400, "RTO exceeds the provisional target")

    reject_sensitive_keys(candidate)
    return {
        "twoPersonApproval": True,
        "timelineOrdered": True,
        "sourceIsolatedBeforeRecovery": True,
        "latestSchemaRestored": True,
        "artifactContainsNoSensitiveKeys": True,
    }


def atomic_write(path: Path, content: bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temporary_name = tempfile.mkstemp(prefix=f".{path.name}.", dir=path.parent)
    try:
        os.fchmod(descriptor, 0o600)
        with os.fdopen(descriptor, "wb") as stream:
            stream.write(content)
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary_name, path)
    except BaseException:
        try:
            os.unlink(temporary_name)
        except FileNotFoundError:
            pass
        raise


def canonical_json(value: object) -> bytes:
    return (json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":")) + "\n").encode("utf-8")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", type=Path, required=True)
    parser.add_argument("--artifact", type=Path, required=True)
    parser.add_argument("--evidence", type=Path, required=True)
    args = parser.parse_args()

    candidate = json.loads(args.input.read_text(encoding="utf-8"))
    require(isinstance(candidate, dict), "candidate must be a JSON object")
    validation = validate(candidate)
    artifact_bytes = canonical_json(candidate)
    artifact_hash = hashlib.sha256(artifact_bytes).hexdigest()
    atomic_write(args.artifact, artifact_bytes)

    sealed_at = datetime.now(timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")
    evidence = {
        "format": "toi-no-mori-dr-failover-evidence-v1",
        "status": "accepted",
        "incidentId": candidate["incidentId"],
        "isSimulated": False,
        "measurementScope": MEASUREMENT_SCOPE,
        "physicalRegionFailover": False,
        "artifactFile": args.artifact.name,
        "artifactHash": f"sha256:{artifact_hash}",
        "sealedAtUtc": sealed_at,
        "validation": validation,
    }
    atomic_write(args.evidence, canonical_json(evidence))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (EvidenceValidationError, json.JSONDecodeError, OSError) as error:
        print(f"DR evidence rejected: {error}", file=sys.stderr)
        raise SystemExit(4) from error
