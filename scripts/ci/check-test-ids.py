#!/usr/bin/env python3
"""Reject duplicate executable test IDs without relying on hand review."""

from __future__ import annotations

import argparse
import json
import re
import sys
from collections import defaultdict
from pathlib import Path


ID = r"TC-(?:ACC|PERF)-MVS01-[0-9]{3}(?:-[A-Z]+)?"
CS_DECLARATION = re.compile(rf'new\("({ID})"')
PY_DECLARATION = re.compile(rf'^\s*\("({ID})"', re.MULTILINE)
SHELL_DECLARATION = re.compile(rf'^# TEST-ID: ({ID})$', re.MULTILINE)
NEW_ID = re.compile(r"TC-(?:ACC|PERF)-MVS01-[0-9]{3}-[A-Z]+")
DEFERRED_TESTS = Path("spec/deferred-tests.json")


def executable_files(root: Path) -> list[Path]:
    files = list((root / "tests").glob("ToiNoMori.*.Tests/**/*.cs"))
    files.extend((root / "tests/stage6r1").glob("*.py"))
    files.append(root / "scripts/test-disaster-recovery.sh")
    return sorted(path for path in files if path.is_file())


def suite(path: Path, root: Path) -> str:
    relative = path.relative_to(root)
    if relative.parts[:2] == ("tests", "stage6r1"):
        return "stage6r1-red"
    if relative.parts and relative.parts[0] == "scripts":
        return "dr-shell"
    return relative.parts[1] if len(relative.parts) > 1 else str(relative)


def collect(root: Path) -> tuple[dict[str, list[tuple[str, Path]]], dict[str, list[Path]]]:
    by_suite: dict[str, list[tuple[str, Path]]] = defaultdict(list)
    new_global: dict[str, list[Path]] = defaultdict(list)
    for path in executable_files(root):
        content = path.read_text(encoding="utf-8")
        if path.suffix == ".cs":
            pattern = CS_DECLARATION
        elif path.suffix == ".sh":
            pattern = SHELL_DECLARATION
        else:
            pattern = PY_DECLARATION
        for test_id in pattern.findall(content):
            by_suite[suite(path, root)].append((test_id, path))
            if NEW_ID.fullmatch(test_id):
                new_global[test_id].append(path)

    deferred_path = root / DEFERRED_TESTS
    deferred = json.loads(deferred_path.read_text(encoding="utf-8"))
    for entry in deferred.get("tests", []):
        test_id = str(entry.get("testId", ""))
        if not NEW_ID.fullmatch(test_id):
            raise ValueError(f"Deferred test ID is invalid or not layer-qualified: {test_id}")
        if entry.get("status") != "not-run":
            raise ValueError(f"Deferred test must have status=not-run: {test_id}")
        for field in ("reasonCode", "reason", "owner", "due"):
            if not str(entry.get(field, "")).strip():
                raise ValueError(f"Deferred test {test_id} is missing {field}")
        new_global[test_id].append(deferred_path)
    return by_suite, new_global


def duplicates(root: Path) -> list[str]:
    by_suite, new_global = collect(root)
    failures: list[str] = []
    for suite_name, occurrences in sorted(by_suite.items()):
        grouped: dict[str, list[Path]] = defaultdict(list)
        for test_id, path in occurrences:
            grouped[test_id].append(path)
        for test_id, paths in sorted(grouped.items()):
            if len(paths) > 1:
                locations = ", ".join(str(path.relative_to(root)) for path in paths)
                failures.append(f"{suite_name}: {test_id} appears {len(paths)} times ({locations})")
    for test_id, paths in sorted(new_global.items()):
        # The Stage 6R-1 Python registry is the single executable source for each
        # new ID. Native behavioural replacements must remove that registry entry.
        if len(paths) > 1:
            locations = ", ".join(str(path.relative_to(root)) for path in paths)
            failures.append(f"global new ID: {test_id} appears {len(paths)} times ({locations})")
    return failures


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[2])
    args = parser.parse_args()
    failures = duplicates(args.root.resolve())
    if failures:
        print("test ID uniqueness: FAILED")
        for failure in failures:
            print(f"- {failure}")
        return 1
    print("test ID uniqueness: PASSED")
    return 0


if __name__ == "__main__":
    sys.exit(main())
