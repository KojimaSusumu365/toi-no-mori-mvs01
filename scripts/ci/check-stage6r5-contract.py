#!/usr/bin/env python3
"""Static and parser contract tests for the Stage 6R-5 full regression workflow."""

from __future__ import annotations

import importlib.util
import re
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
WORKFLOW = ROOT / ".github/workflows/stage6r4c-nonroot-postgresql.yml"
WRAPPER = ROOT / "scripts/ci/run-stage6r5-full-regression-ci.sh"
EVIDENCE_WRITER = ROOT / "scripts/ci/write-stage6r5-evidence.py"
PINNED_ACTION = re.compile(r"^\s*uses:\s+[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+@[0-9a-f]{40}(?:\s+#.*)?$", re.MULTILINE)
ANY_ACTION = re.compile(r"^\s*uses:\s+([^\s]+)", re.MULTILINE)


def load_evidence_module():
    spec = importlib.util.spec_from_file_location("stage6r5_evidence", EVIDENCE_WRITER)
    if spec is None or spec.loader is None:
        raise RuntimeError("Could not load the Stage 6R-5 evidence writer.")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


def synthetic_log(api_passed: int = 37, dr_passed: int = 4) -> str:
    api_failed = 37 - api_passed
    dr_failed = 4 - dr_passed
    return "\n".join(
        [
            "test ID uniqueness: PASSED",
            "    0 Warning(s)",
            "    0 Error(s)",
            "# ToiNoMori.Domain specification tests",
            "# result: 12 passed; 0 failed; 12 total",
            "# ToiNoMori.Api specification tests",
            f"# result: {api_passed} passed; {api_failed} failed; 37 total",
            "# ToiNoMori mobile web specification tests",
            "# result: 6 passed; 0 failed; 6 total",
            "# ToiNoMori OIDC browser protocol E2E tests",
            "# result: 7 passed; 0 failed; 7 total",
            "# ToiNoMori PostgreSQL integration tests",
            "# result: 10 passed; 0 failed; 10 total",
            "# ToiNoMori disaster-recovery specification tests",
            f"# result: {dr_passed} passed; {dr_failed} failed; 4 total",
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
    checks.append(("workflow invokes the full regression wrapper and immutable artifact", "run-stage6r5-full-regression-ci.sh" in workflow and "stage6r5-full-regression-evidence" in workflow))
    checks.append(("wrapper preserves root guard and executes all native suites", '[[ "$RUNNER_UID" -eq 0 ]]' in wrapper and "test-all.sh" in wrapper and "check-test-ids.sh" in wrapper))
    checks.append(("wrapper captures DR evidence without claiming cloud failover", "MVS01_DR_EVIDENCE_DIR" in wrapper and "execution-mode native" in wrapper))

    module = load_evidence_module()
    log_text = synthetic_log()
    suites = module.parse_suites(log_text)
    accepted, acceptance = module.acceptance_status(
        gate_exit_code=0,
        uid=1001,
        execution_mode="native",
        suites=suites,
        test_ids_unique=True,
        build_clean=True,
    )
    simulated, _ = module.acceptance_status(
        gate_exit_code=0,
        uid=1001,
        execution_mode="contract-test",
        suites=suites,
        test_ids_unique=True,
        build_clean=True,
    )
    root_run, _ = module.acceptance_status(
        gate_exit_code=0,
        uid=0,
        execution_mode="native",
        suites=suites,
        test_ids_unique=True,
        build_clean=True,
    )
    incomplete, _ = module.acceptance_status(
        gate_exit_code=1,
        uid=1001,
        execution_mode="native",
        suites=module.parse_suites(synthetic_log(dr_passed=3)),
        test_ids_unique=True,
        build_clean=True,
    )
    checks.append(("evidence accepts only exact-count clean native non-root GREEN", accepted and all(acceptance.values()) and not simulated and not root_run and not incomplete))

    print(f"1..{len(checks)}")
    failed = 0
    for index, (name, passed) in enumerate(checks, start=1):
        if passed:
            print(f"ok {index} - CFG-ST6R5-{index:03d} {name}")
        else:
            failed += 1
            print(f"not ok {index} - CFG-ST6R5-{index:03d} {name}")
    print(f"# result: {len(checks) - failed} passed; {failed} failed; {len(checks)} total")
    return 0 if failed == 0 else 1


if __name__ == "__main__":
    raise SystemExit(main())
