#!/usr/bin/env python3
"""Run and seal the 40 AUTO-T bootstrap acceptance cases."""

from __future__ import annotations

import hashlib
import json
import os
import subprocess
import sys
import unittest
from datetime import datetime, timezone
from pathlib import Path

# Keep the standalone acceptance runner independent of caller PYTHONPATH.
REPO_ROOT = Path(__file__).resolve().parents[2]
if str(REPO_ROOT) not in sys.path:
    sys.path.insert(0, str(REPO_ROOT))

from scripts.ai_controller.core import ROOT, content_hash, validate_control_plane
from tests.automation.test_github_autodrive_controller import GitHubAutodriveControllerTests


class EvidenceResult(unittest.TestResult):
    def __init__(self):
        super().__init__()
        self.cases = []

    @staticmethod
    def identifier(test) -> str:
        return getattr(getattr(test, test._testMethodName), "auto_test_id")

    def addSuccess(self, test):
        super().addSuccess(test)
        self.cases.append({"id": self.identifier(test), "result": "GREEN"})

    def addFailure(self, test, err):
        super().addFailure(test, err)
        self.cases.append({"id": self.identifier(test), "result": "RED", "detail": self._exc_info_to_string(err, test)})

    def addError(self, test, err):
        super().addError(test, err)
        self.cases.append({"id": self.identifier(test), "result": "RED", "detail": self._exc_info_to_string(err, test)})


def git_value(*args: str) -> str:
    return subprocess.check_output(["git", *args], cwd=ROOT, text=True).strip()


def main() -> int:
    suite = unittest.defaultTestLoader.loadTestsFromTestCase(GitHubAutodriveControllerTests)
    result = EvidenceResult()
    suite.run(result)
    cases = sorted(result.cases, key=lambda item: item["id"])
    expected = {f"AUTO-T{number:02d}" for number in range(1, 40) if number != 9} | {"AUTO-T09a", "AUTO-T09b"}
    actual = {item["id"] for item in cases}
    complete = actual == expected and len(cases) == 40
    passed = sum(item["result"] == "GREEN" for item in cases)
    registries = validate_control_plane()
    evidence = {
        "protocol": "QF-AI-COLLAB-v5",
        "schema_version": 1,
        "mode": "BOOTSTRAP_DISABLED",
        "run_id": os.environ.get("GITHUB_RUN_ID", "LOCAL"),
        "job_id": os.environ.get("GITHUB_JOB", "LOCAL"),
        "workflow_sha": os.environ.get("GITHUB_WORKFLOW_SHA", git_value("rev-parse", "HEAD")),
        "tested_commit_sha": os.environ.get("GITHUB_SHA", git_value("rev-parse", "HEAD")),
        "tree_sha": git_value("rev-parse", "HEAD^{tree}"),
        "generated_at": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
        "expected": 40,
        "passed": passed,
        "failed": 40 - passed,
        "complete": complete,
        "cases": cases,
        "registry_hashes": {
            "gate_checks": registries["gate_checks"]["sha256"],
            "work_order_preconditions": registries["work_order_preconditions"]["sha256"],
            "stop_conditions": registries["stop_conditions"]["sha256"],
        },
        "review_request_hash": content_hash({"mode": "BOOTSTRAP_DISABLED", "tests": 40}),
        "durable_review_result_record_hash": None,
    }
    evidence["artifact_sha256"] = content_hash(evidence)
    output = Path(os.environ.get("QF_AUTOMATION_EVIDENCE", ROOT / "artifacts/automation/controller-test-evidence.json"))
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(evidence, ensure_ascii=False, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    for item in cases:
        print(f"{'ok' if item['result'] == 'GREEN' else 'not ok'} - {item['id']}")
    print(f"# result: {passed} passed; {40 - passed} failed; 40 total")
    print(f"# evidence: {output}")
    if not complete or not result.wasSuccessful():
        for _, detail in result.failures + result.errors:
            print(detail, file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
