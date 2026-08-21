#!/usr/bin/env python3
"""Static and parser contracts for the Stage 6R-7 security workflow."""

from __future__ import annotations

import importlib.util
import re
import sys
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
WORKFLOW = ROOT / ".github/workflows/stage6r7-append-only.yml"
WRAPPER = ROOT / "scripts/ci/run-stage6r7-full-regression-ci.sh"
EVIDENCE_WRITER = ROOT / "scripts/ci/write-stage6r7-evidence.py"
PINNED_ACTION = re.compile(
    r"^\s*uses:\s+[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+@[0-9a-f]{40}(?:\s+#.*)?$",
    re.MULTILINE,
)
ANY_ACTION = re.compile(r"^\s*uses:\s+([^\s]+)", re.MULTILINE)


def load_evidence_module():
    spec = importlib.util.spec_from_file_location("stage6r7_evidence", EVIDENCE_WRITER)
    if spec is None or spec.loader is None:
        raise RuntimeError("Could not load the Stage 6R-7 evidence writer.")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


def synthetic_log(api_passed: int = 40, pg_passed: int = 12) -> str:
    return "\n".join(
        [
            "test ID uniqueness: PASSED",
            "    0 Warning(s)",
            "    0 Error(s)",
            "# ToiNoMori.Domain specification tests",
            "# result: 12 passed; 0 failed; 12 total",
            "# ToiNoMori.Api specification tests",
            f"# result: {api_passed} passed; {40 - api_passed} failed; 40 total",
            "# ToiNoMori mobile web specification tests",
            "# result: 6 passed; 0 failed; 6 total",
            "# ToiNoMori OIDC browser protocol E2E tests",
            "# result: 7 passed; 0 failed; 7 total",
            "# ToiNoMori PostgreSQL integration tests",
            f"# result: {pg_passed} passed; {12 - pg_passed} failed; 12 total",
            "# ToiNoMori disaster-recovery specification tests",
            "# result: 4 passed; 0 failed; 4 total",
        ]
    )


def main() -> int:
    workflow = WORKFLOW.read_text(encoding="utf-8")
    wrapper = WRAPPER.read_text(encoding="utf-8")
    checks: list[tuple[str, bool]] = []
    actions = ANY_ACTION.findall(workflow)
    pinned = PINNED_ACTION.findall(workflow)
    checks.append(("four external actions are full-SHA pinned", len(actions) == 4 and len(pinned) == 4))
    checks.append(("workflow is read-only and avoids privileged triggers", "permissions:\n  contents: read" in workflow and "pull_request_target" not in workflow and "sudo " not in workflow))
    checks.append(("workflow fixes Ubuntu and checks non-root", "runs-on: ubuntu-24.04" in workflow and 'test "$(id -u)" -ne 0' in workflow))
    checks.append(("workflow invokes the Stage 6R-7 wrapper and immutable artifact", "run-stage6r7-full-regression-ci.sh" in workflow and "stage6r7-append-only-evidence" in workflow))
    checks.append(("wrapper is fail-closed and runs every native suite", '[[ "$RUNNER_UID" -eq 0 ]]' in wrapper and "test-all.sh" in wrapper and "check-test-ids.sh" in wrapper))

    module = load_evidence_module()
    suites = module.parse_suites(synthetic_log())
    accepted, acceptance = module.acceptance_status(
        gate_exit_code=0,
        uid=1001,
        execution_mode="native",
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
    simulated, _ = module.acceptance_status(
        gate_exit_code=0,
        uid=1001,
        execution_mode="contract-test",
        suites=suites,
        test_ids_unique=True,
        build_clean=True,
    )
    incomplete, _ = module.acceptance_status(
        gate_exit_code=1,
        uid=1001,
        execution_mode="native",
        suites=module.parse_suites(synthetic_log(pg_passed=10)),
        test_ids_unique=True,
        build_clean=True,
    )
    checks.append(("evidence accepts only exact 81-test native non-root GREEN", accepted and all(acceptance.values()) and not root_run and not simulated and not incomplete))

    print(f"1..{len(checks)}")
    failed = 0
    for index, (name, passed) in enumerate(checks, start=1):
        if passed:
            print(f"ok {index} - CFG-ST6R7-{index:03d} {name}")
        else:
            failed += 1
            print(f"not ok {index} - CFG-ST6R7-{index:03d} {name}")
    print(f"# result: {len(checks) - failed} passed; {failed} failed; {len(checks)} total")
    return 0 if failed == 0 else 1


if __name__ == "__main__":
    raise SystemExit(main())
