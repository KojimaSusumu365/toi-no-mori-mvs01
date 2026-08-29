#!/usr/bin/env python3
"""Create machine-readable evidence for the Stage 6R-4C native DB gate."""

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


@dataclass(frozen=True)
class SuiteContract:
    key: str
    heading: str
    expected: int


SUITES = (
    SuiteContract("api", "# ToiNoMori.Api specification tests", 37),
    SuiteContract("postgresql", "# ToiNoMori PostgreSQL integration tests", 10),
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
            cast_list = results[current][collection]
            if isinstance(cast_list, list):
                cast_list.append(test_match.group(2))
            continue
        result_match = RESULT.match(line)
        if result_match:
            passed, failed, total = (int(value) for value in result_match.groups())
            expected = results[current]["expected"]
            results[current].update(
                passed=passed,
                failed=failed,
                total=total,
                status="passed" if failed == 0 and total == expected and passed == expected else "failed",
            )
            current = None

    return results


def acceptance_status(
    *,
    gate_exit_code: int,
    uid: int,
    execution_mode: str,
    suites: dict[str, dict[str, object]],
) -> tuple[bool, dict[str, bool]]:
    checks = {
        "nonRootRunner": uid != 0,
        "nativeExecution": execution_mode == "native",
        "api37Of37": suites["api"]["status"] == "passed",
        "postgresql10Of10": suites["postgresql"]["status"] == "passed",
        "gateExitCodeZero": gate_exit_code == 0,
    }
    return all(checks.values()), checks


def build_evidence(args: argparse.Namespace, log_bytes: bytes) -> dict[str, object]:
    log_text = log_bytes.decode("utf-8", errors="replace")
    suites = parse_suites(log_text)
    accepted, checks = acceptance_status(
        gate_exit_code=args.gate_exit_code,
        uid=args.uid,
        execution_mode=args.execution_mode,
        suites=suites,
    )
    is_github = os.environ.get("GITHUB_ACTIONS") == "true"
    return {
        "schemaVersion": "1.0",
        "stage": "6R-4C",
        "gate": "nonroot-native-postgresql",
        "status": "accepted" if accepted else "rejected",
        "isSimulated": args.execution_mode != "native",
        "executionMode": args.execution_mode,
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
            "provider": "GitHub Actions" if is_github else "local",
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
    assert isinstance(suites, dict)
    checks = evidence["acceptance"]
    assert isinstance(checks, dict)
    lines = [
        "# Stage 6R-4C non-root PostgreSQL CI",
        "",
        f"判定: **{evidence['status']}**",
        "",
        "| Gate | 結果 |",
        "|---|---|",
    ]
    for label, key in (("API", "api"), ("PostgreSQL", "postgresql")):
        suite = suites[key]
        assert isinstance(suite, dict)
        total = suite["total"] if suite["total"] is not None else "未実行"
        passed = suite["passed"] if suite["passed"] is not None else 0
        lines.append(f"| {label} | {passed}/{total} ({suite['status']}) |")
    lines.extend(
        [
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
