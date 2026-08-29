#!/usr/bin/env python3
"""Static and parser contracts for the Stage 6R-11 Town-readiness workflow."""

from __future__ import annotations

import importlib.util
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
WORKFLOW = ROOT / ".github/workflows/stage6r11-town-readiness.yml"
WRAPPER = ROOT / "scripts/ci/run-stage6r11-town-readiness-ci.sh"
EVIDENCE_WRITER = ROOT / "scripts/ci/write-stage6r11-evidence.py"
TEST_PROJECT = ROOT / "tests/ToiNoMori.TownReadiness.Tests/Program.cs"
TEST_SCRIPT = ROOT / "scripts/test.sh"
SOLUTION = ROOT / "ToiNoMori.Mvs01.slnx"
PINNED_ACTION = re.compile(
    r"^\s*uses:\s+[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+@[0-9a-f]{40}(?:\s+#.*)?$",
    re.MULTILINE,
)
ANY_ACTION = re.compile(r"^\s*uses:\s+([^\s]+)", re.MULTILINE)


def load_evidence_module():
    spec = importlib.util.spec_from_file_location("stage6r11_evidence", EVIDENCE_WRITER)
    if spec is None or spec.loader is None:
        raise RuntimeError("Could not load the Stage 6R-11 evidence writer.")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


def synthetic_log(readiness_passed: int = 5) -> str:
    return "\n".join(
        [
            "test ID uniqueness: PASSED",
            "    0 Warning(s)",
            "    0 Error(s)",
            "# ToiNoMori.Domain specification tests",
            "# result: 12 passed; 0 failed; 12 total",
            "# ToiNoMori.Api specification tests",
            "# result: 41 passed; 0 failed; 41 total",
            "# ToiNoMori mobile web specification tests",
            "# result: 7 passed; 0 failed; 7 total",
            "# ToiNoMori OIDC browser protocol E2E tests",
            "# result: 8 passed; 0 failed; 8 total",
            "# ToiNoMori town-readiness specification tests",
            f"# result: {readiness_passed} passed; {5 - readiness_passed} failed; 5 total",
            "# ToiNoMori PostgreSQL integration tests",
            "# result: 12 passed; 0 failed; 12 total",
            "# ToiNoMori disaster-recovery specification tests",
            "# result: 5 passed; 0 failed; 5 total",
        ]
    )


def main() -> int:
    workflow = WORKFLOW.read_text(encoding="utf-8")
    wrapper = WRAPPER.read_text(encoding="utf-8")
    readiness = TEST_PROJECT.read_text(encoding="utf-8")
    test_script = TEST_SCRIPT.read_text(encoding="utf-8")
    solution = SOLUTION.read_text(encoding="utf-8")
    checks: list[tuple[str, bool]] = []

    actions = ANY_ACTION.findall(workflow)
    pinned = PINNED_ACTION.findall(workflow)
    checks.append(("four external actions are full-SHA pinned", len(actions) == 4 and len(pinned) == 4))
    checks.append(("workflow is read-only, non-root, and avoids privileged triggers", "permissions:\n  contents: read" in workflow and "pull_request_target" not in workflow and "sudo " not in workflow and 'test "$(id -u)" -ne 0' in workflow))
    checks.append(("workflow invokes the Stage 6R-11 fail-closed wrapper and immutable evidence artifact", "run-stage6r11-town-readiness-ci.sh" in workflow and "stage6r11-town-readiness-evidence" in workflow))
    checks.append(("readiness suite is part of both the build graph and standard regression", "ToiNoMori.TownReadiness.Tests" in solution and "ToiNoMori.TownReadiness.Tests" in test_script))
    checks.append(("readiness suite covers all five Town-readiness behavioural gates", all(test_id in readiness for test_id in ["TC-ACC-MVS01-082-TR", "TC-ACC-MVS01-083-TR", "TC-ACC-MVS01-084-TR", "TC-ACC-MVS01-085-TR", "TC-ACC-MVS01-086-TR"])))
    checks.append(("wrapper is fail-closed and runs ID uniqueness plus all native regression suites", '[[ "$RUNNER_UID" -eq 0 ]]' in wrapper and "test-all.sh" in wrapper and "check-test-ids.sh" in wrapper))

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
        suites=module.parse_suites(synthetic_log(readiness_passed=4)),
        test_ids_unique=True,
        build_clean=True,
    )
    checks.append(("evidence accepts only exact 90-test native non-root GREEN", accepted and all(acceptance.values()) and not root_run and not simulated and not incomplete))

    print(f"1..{len(checks)}")
    failed = 0
    for index, (name, passed) in enumerate(checks, start=1):
        if passed:
            print(f"ok {index} - CFG-ST6R11-{index:03d} {name}")
        else:
            failed += 1
            print(f"not ok {index} - CFG-ST6R11-{index:03d} {name}")
    print(f"# result: {len(checks) - failed} passed; {failed} failed; {len(checks)} total")
    return 0 if failed == 0 else 1


if __name__ == "__main__":
    raise SystemExit(main())
