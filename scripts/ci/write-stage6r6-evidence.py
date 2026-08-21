#!/usr/bin/env python3
"""Create fail-closed evidence for the Stage 6R-6 native 80-test gate."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import platform
import re
from dataclasses import dataclass
from pathlib import Path


RESULT = re.compile(r"^# result: (\d+) passed; (\d+) failed; (\d+) total$")
TEST_LINE = re.compile(r"^(ok|not ok) \d+ - (TC-(?:ACC|PERF)-MVS01-[0-9]{3}(?:-[A-Z]+)?)")
WARNING_COUNT = re.compile(r"^\s*(\d+) Warning\(s\)$", re.MULTILINE)
ERROR_COUNT = re.compile(r"^\s*(\d+) Error\(s\)$", re.MULTILINE)


@dataclass(frozen=True)
class SuiteContract:
    key: str
    label: str
    heading: str
    expected: int


SUITES = (
    SuiteContract("domain", "Domain", "# ToiNoMori.Domain specification tests", 12),
    SuiteContract("api", "API", "# ToiNoMori.Api specification tests", 40),
    SuiteContract("mobile", "Mobile", "# ToiNoMori mobile web specification tests", 6),
    SuiteContract("oidc", "OIDC E2E", "# ToiNoMori OIDC browser protocol E2E tests", 7),
    SuiteContract("postgresql", "PostgreSQL", "# ToiNoMori PostgreSQL integration tests", 11),
    SuiteContract("dr", "DR", "# ToiNoMori disaster-recovery specification tests", 4),
)


def parse_suites(log_text: str) -> dict[str, dict[str, object]]:
    results: dict[str, dict[str, object]] = {
        suite.key: {
            "expected": suite.expected,
            "passed": None,
            "failed": None,
            "total": None,
            "passedTestIds": [],
            "failedTestIds": [],
            "status": "not-run",
        }
        for suite in SUITES
    }
    headings = {suite.heading: suite.key for suite in SUITES}
    current: str | None = None
    for line in log_text.splitlines():
        if line in headings:
            current = headings[line]
            continue
        if current is None:
            continue
        test_match = TEST_LINE.match(line)
        if test_match:
            collection = "passedTestIds" if test_match.group(1) == "ok" else "failedTestIds"
            values = results[current][collection]
            if isinstance(values, list):
                values.append(test_match.group(2))
            continue
        result_match = RESULT.match(line)
        if result_match:
            passed, failed, total = (int(value) for value in result_match.groups())
            expected = results[current]["expected"]
            results[current].update(
                passed=passed,
                failed=failed,
                total=total,
                status="passed" if passed == expected and failed == 0 and total == expected else "failed",
            )
            current = None
    return results


def acceptance_status(
    *,
    gate_exit_code: int,
    uid: int,
    execution_mode: str,
    suites: dict[str, dict[str, object]],
    test_ids_unique: bool,
    build_clean: bool,
) -> tuple[bool, dict[str, bool]]:
    checks = {
        "nonRootRunner": uid != 0,
        "nativeExecution": execution_mode == "native",
        "testIdsUnique": test_ids_unique,
        "buildClean": build_clean,
        **{
            f"{suite.key}{suite.expected}Of{suite.expected}": suites[suite.key]["status"] == "passed"
            for suite in SUITES
        },
        "nativeTotal80Of80": sum(suite.expected for suite in SUITES) == 80
        and all(suites[suite.key]["status"] == "passed" for suite in SUITES),
        "gateExitCodeZero": gate_exit_code == 0,
    }
    return all(checks.values()), checks


def build_evidence(args: argparse.Namespace, log_bytes: bytes) -> dict[str, object]:
    log_text = log_bytes.decode("utf-8", errors="replace")
    suites = parse_suites(log_text)
    warning_counts = [int(value) for value in WARNING_COUNT.findall(log_text)]
    error_counts = [int(value) for value in ERROR_COUNT.findall(log_text)]
    build_clean = bool(warning_counts and error_counts) and all(
        value == 0 for value in warning_counts + error_counts
    )
    test_ids_unique = "test ID uniqueness: PASSED" in log_text
    accepted, checks = acceptance_status(
        gate_exit_code=args.gate_exit_code,
        uid=args.uid,
        execution_mode=args.execution_mode,
        suites=suites,
        test_ids_unique=test_ids_unique,
        build_clean=build_clean,
    )
    return {
        "schemaVersion": "1.0",
        "stage": "6R-6",
        "gate": "platform-security-full-regression",
        "status": "accepted" if accepted else "rejected",
        "isSimulated": args.execution_mode != "native",
        "executionMode": args.execution_mode,
        "measurementScope": "native process integration; PostgreSQL role boundary and local isolated DR, not cloud-region failover",
        "startedAtUtc": args.started_at,
        "finishedAtUtc": args.finished_at,
        "source": {
            "repository": os.environ.get("GITHUB_REPOSITORY", "local-workspace"),
            "commit": os.environ.get("GITHUB_SHA", "unknown"),
            "ref": os.environ.get("GITHUB_REF", "unknown"),
            "workflow": os.environ.get("GITHUB_WORKFLOW", "local"),
            "runId": os.environ.get("GITHUB_RUN_ID", "unknown"),
            "runAttempt": os.environ.get("GITHUB_RUN_ATTEMPT", "unknown"),
        },
        "runner": {
            "provider": "GitHub Actions" if os.environ.get("GITHUB_ACTIONS") == "true" else "local",
            "operatingSystem": platform.platform(),
            "architecture": platform.machine(),
            "user": args.user,
            "uid": args.uid,
            "isNonRoot": args.uid != 0,
        },
        "toolchain": {
            "dotnetSdk": args.dotnet_version,
            "postgresql": args.postgresql_version,
        },
        "suites": suites,
        "gateExitCode": args.gate_exit_code,
        "acceptance": checks,
        "log": {
            "file": args.log.name,
            "sha256": hashlib.sha256(log_bytes).hexdigest(),
        },
    }


def write_summary(path: Path, evidence: dict[str, object]) -> None:
    suites = evidence["suites"]
    checks = evidence["acceptance"]
    assert isinstance(suites, dict)
    assert isinstance(checks, dict)
    lines = [
        "# Stage 6R-6 Platform Security full regression",
        "",
        f"判定: **{evidence['status']}**",
        "",
        "| Gate | 結果 |",
        "|---|---|",
    ]
    for suite in SUITES:
        result = suites[suite.key]
        assert isinstance(result, dict)
        total = result["total"] if result["total"] is not None else "未実行"
        passed = result["passed"] if result["passed"] is not None else 0
        lines.append(f"| {suite.label} | {passed}/{total} ({result['status']}) |")
    lines.extend(
        [
            f"| Native合計 | 80/80={checks['nativeTotal80Of80']} |",
            f"| 試験ID一意性 | {checks['testIdsUnique']} |",
            f"| Build警告0・エラー0 | {checks['buildClean']} |",
            f"| 非root runner | {checks['nonRootRunner']} |",
            f"| native実行 | {checks['nativeExecution']} |",
            f"| gate終了コード | {evidence['gateExitCode']} |",
            "",
            f"Log SHA-256: `{evidence['log']['sha256']}`",
            "",
        ]
    )
    path.write_text("\n".join(lines), encoding="utf-8")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--log", type=Path, required=True)
    parser.add_argument("--json", type=Path, required=True)
    parser.add_argument("--summary", type=Path, required=True)
    parser.add_argument("--started-at", required=True)
    parser.add_argument("--finished-at", required=True)
    parser.add_argument("--uid", type=int, required=True)
    parser.add_argument("--user", required=True)
    parser.add_argument("--gate-exit-code", type=int, required=True)
    parser.add_argument("--execution-mode", choices=("native", "contract-test"), required=True)
    parser.add_argument("--dotnet-version", required=True)
    parser.add_argument("--postgresql-version", required=True)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    log_bytes = args.log.read_bytes()
    evidence = build_evidence(args, log_bytes)
    args.json.parent.mkdir(parents=True, exist_ok=True)
    args.json.write_text(json.dumps(evidence, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    write_summary(args.summary, evidence)
    return 0 if evidence["status"] == "accepted" else 1


if __name__ == "__main__":
    raise SystemExit(main())
