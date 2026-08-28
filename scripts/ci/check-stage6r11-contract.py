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
PUBLIC_READ_CONTEXT = ROOT / "src/ToiNoMori.Api/PublicReadTenantContext.cs"
API_TESTS = ROOT / "tests/ToiNoMori.Api.Tests/Program.cs"
DEFERRED_TESTS = ROOT / "spec/deferred-tests.json"
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


def synthetic_log(
    readiness_passed: int = 5,
    *,
    include_town_readiness: bool = True,
) -> str:
    lines = [
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
    ]
    if include_town_readiness:
        lines.extend(
            [
                "# ToiNoMori town-readiness specification tests",
                f"# result: {readiness_passed} passed; {5 - readiness_passed} failed; 5 total",
            ]
        )
    lines.extend(
        [
            "# ToiNoMori PostgreSQL integration tests",
            "# result: 12 passed; 0 failed; 12 total",
            "# ToiNoMori disaster-recovery specification tests",
            "# result: 5 passed; 0 failed; 5 total",
        ]
    )
    return "\n".join(lines)

def main() -> int:
    workflow = WORKFLOW.read_text(encoding="utf-8")
    wrapper = WRAPPER.read_text(encoding="utf-8")
    readiness = TEST_PROJECT.read_text(encoding="utf-8")
    test_script = TEST_SCRIPT.read_text(encoding="utf-8")
    solution = SOLUTION.read_text(encoding="utf-8")
    public_read_context = PUBLIC_READ_CONTEXT.read_text(encoding="utf-8")
    api_tests = API_TESTS.read_text(encoding="utf-8")
    deferred_tests = DEFERRED_TESTS.read_text(encoding="utf-8")
    evidence_writer = EVIDENCE_WRITER.read_text(encoding="utf-8")
    checks: list[tuple[str, bool]] = []

    actions = ANY_ACTION.findall(workflow)
    pinned = PINNED_ACTION.findall(workflow)
    checks.append(("four external actions are full-SHA pinned", len(actions) == 4 and len(pinned) == 4))
    checks.append(("workflow is read-only, non-root, and avoids privileged triggers", "permissions:\n  contents: read" in workflow and "pull_request_target" not in workflow and "sudo " not in workflow and 'test "$(id -u)" -ne 0' in workflow))
    checks.append(("workflow invokes the Stage 6R-11 fail-closed wrapper and immutable evidence artifact", "run-stage6r11-town-readiness-ci.sh" in workflow and "stage6r11-town-readiness-evidence" in workflow))
    checks.append(("readiness suite is part of both the build graph and standard regression", "ToiNoMori.TownReadiness.Tests" in solution and "ToiNoMori.TownReadiness.Tests" in test_script))
    checks.append(("readiness suite covers all five Town-readiness behavioural gates", all(test_id in readiness for test_id in ["TC-ACC-MVS01-082-TR", "TC-ACC-MVS01-083-TR", "TC-ACC-MVS01-084-TR", "TC-ACC-MVS01-085-TR", "TC-ACC-MVS01-086-TR"])))
    checks.append(("wrapper is fail-closed and runs ID uniqueness plus all native regression suites", '[[ "$RUNNER_UID" -eq 0 ]]' in wrapper and "test-all.sh" in wrapper and "check-test-ids.sh" in wrapper))
    checks.append(("Public Read startup gate permits exactly one configured tenant without a BYPASSRLS query", all(token in public_read_context for token in ["single_tenant", "configuredTenantIds.Length != 1", "Architecture Gate", "System Architect"]) and "BYPASSRLS" not in public_read_context))
    checks.append(("TC-065 proves invalid mode, second tenant, and invalid UUID fail startup", all(token in api_tests for token in ["PublicRead:Mode=multi_tenant", "PublicRead:TenantIds:1=", "PublicRead:TenantIds:0=not-a-uuid"])))
    checks.append(("cross-audience negative test is formally registered not-run", all(token in deferred_tests for token in ["TC-ACC-MVS01-087-OIDC", '"status": "not-run"', '"owner": "System Architect"', '"due": "VT-1 start"'])))
    checks.append(("evidence uses dynamic totals and typed source identity", all(token in evidence_writer for token in ["expectedTotal", "passedTotal", "registeredSuitesComplete", "totalsMatch", "testedCommit", "authoritativeBranchHead", "commitRelationship"]) and "nativeTotal90Of90" not in evidence_writer))
    checks.append(("workflow verifies the tested commit relationship", all(token in workflow for token in ["MVS01_TESTED_COMMIT", "MVS01_AUTHORITATIVE_BRANCH_HEAD", "MVS01_COMMIT_RELATIONSHIP", "Verify evidence source identity"])))

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
    missing_suite, _ = module.acceptance_status(
        gate_exit_code=0,
        uid=1001,
        execution_mode="native",
        suites=module.parse_suites(synthetic_log(include_town_readiness=False)),
        test_ids_unique=True,
        build_clean=True,
    )
    checks.append(("evidence accepts only complete 90-test native non-root GREEN", accepted and all(acceptance.values()) and not root_run and not simulated and not incomplete and not missing_suite))

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
