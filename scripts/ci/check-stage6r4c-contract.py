#!/usr/bin/env python3
"""Static and parser contract tests for the Stage 6R-4C workflow."""

from __future__ import annotations

import importlib.util
import re
import sys
from argparse import Namespace
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
WORKFLOW = ROOT / ".github/workflows/stage6r4c-nonroot-postgresql.yml"
WRAPPER = ROOT / "scripts/ci/run-stage6r4c-db-security-ci.sh"
EVIDENCE_WRITER = ROOT / "scripts/ci/write-stage6r4c-evidence.py"
PINNED_ACTION = re.compile(r"^\s*uses:\s+[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+@[0-9a-f]{40}(?:\s+#.*)?$", re.MULTILINE)
ANY_ACTION = re.compile(r"^\s*uses:\s+([^\s]+)", re.MULTILINE)


def load_evidence_module():
    spec = importlib.util.spec_from_file_location("stage6r4c_evidence", EVIDENCE_WRITER)
    if spec is None or spec.loader is None:
        raise RuntimeError("Could not load the evidence writer.")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


def synthetic_log(api_passed: int = 37, pg_passed: int = 10) -> str:
    api_failed = 37 - api_passed
    pg_failed = 10 - pg_passed
    return "\n".join(
        [
            "# ToiNoMori.Api specification tests",
            "1..37",
            f"# result: {api_passed} passed; {api_failed} failed; 37 total",
            "# ToiNoMori PostgreSQL integration tests",
            "1..10",
            f"# result: {pg_passed} passed; {pg_failed} failed; 10 total",
        ]
    )


def main() -> int:
    workflow = WORKFLOW.read_text(encoding="utf-8")
    wrapper = WRAPPER.read_text(encoding="utf-8")
    checks: list[tuple[str, bool]] = []

    action_lines = ANY_ACTION.findall(workflow)
    pinned_lines = PINNED_ACTION.findall(workflow)
    checks.append(("external actions are pinned to full commit SHA", len(action_lines) == 4 and len(pinned_lines) == 4))
    checks.append(("workflow uses read-only contents permission", "permissions:\n  contents: read" in workflow))
    checks.append(("workflow avoids pull_request_target and sudo", "pull_request_target" not in workflow and "sudo " not in workflow))
    checks.append(("workflow fixes Ubuntu and checks non-root UID", "runs-on: ubuntu-24.04" in workflow and 'test "$(id -u)" -ne 0' in workflow))
    checks.append(("wrapper preserves the root guard and invokes the mandatory gate", '[[ "$RUNNER_UID" -eq 0 ]]' in wrapper and "test-stage6r4-db-security.sh" in wrapper))

    module = load_evidence_module()
    parsed = module.parse_suites(synthetic_log())
    accepted, acceptance = module.acceptance_status(
        gate_exit_code=0,
        uid=1001,
        execution_mode="native",
        suites=parsed,
    )
    simulated, _ = module.acceptance_status(
        gate_exit_code=0,
        uid=1001,
        execution_mode="contract-test",
        suites=parsed,
    )
    root_run, _ = module.acceptance_status(
        gate_exit_code=0,
        uid=0,
        execution_mode="native",
        suites=parsed,
    )
    incomplete, _ = module.acceptance_status(
        gate_exit_code=1,
        uid=1001,
        execution_mode="native",
        suites=module.parse_suites(synthetic_log(pg_passed=9)),
    )
    checks.append(("evidence accepts only native non-root exact-count GREEN", accepted and all(acceptance.values()) and not simulated and not root_run and not incomplete))

    print(f"1..{len(checks)}")
    failed = 0
    for index, (name, passed) in enumerate(checks, start=1):
        if passed:
            print(f"ok {index} - CFG-ST6R4C-{index:03d} {name}")
        else:
            failed += 1
            print(f"not ok {index} - CFG-ST6R4C-{index:03d} {name}")
    print(f"# result: {len(checks) - failed} passed; {failed} failed; {len(checks)} total")
    return 0 if failed == 0 else 1


if __name__ == "__main__":
    raise SystemExit(main())
