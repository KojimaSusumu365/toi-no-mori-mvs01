#!/usr/bin/env python3
"""Command-line entry point for the Question Forest auto-drive controller."""

from __future__ import annotations

import argparse
import json
import sys
from datetime import datetime, timezone
from pathlib import Path

# Direct script execution places ``scripts/`` rather than the repository root
# on sys.path.  Add the trusted repository root before importing the package.
REPO_ROOT = Path(__file__).resolve().parents[1]
if str(REPO_ROOT) not in sys.path:
    sys.path.insert(0, str(REPO_ROOT))

from scripts.ai_controller.core import (
    ROOT,
    ControllerError,
    canonical_json,
    load_json_yaml,
    load_registry,
    loop_transition,
    required_check_names,
    route_origin,
    validate_control_plane,
    validate_manufacturing_result,
    validate_review_gate,
    validate_scope,
    validate_threat_baseline,
    validate_work_order,
)


def _read(path: str) -> dict:
    return load_json_yaml(Path(path))


def _write_result(result: dict, output: str | None) -> None:
    rendered = json.dumps(result, ensure_ascii=False, indent=2, sort_keys=True) + "\n"
    if output:
        Path(output).parent.mkdir(parents=True, exist_ok=True)
        Path(output).write_text(rendered, encoding="utf-8")
    print(rendered, end="")


def command_preflight(args: argparse.Namespace) -> dict:
    registries = validate_control_plane()
    baseline = load_json_yaml(ROOT / "docs/governance/threat-model/GITHUB-AUTOMATION.yml")
    baseline_decision = validate_threat_baseline(baseline)
    if not baseline_decision.accepted:
        raise ControllerError(f"unsafe baseline: {baseline_decision.reasons}")
    return {
        "protocol": "QF-AI-COLLAB-v5",
        "state": baseline.get("automation", {}).get("phase", "UNKNOWN"),
        "registries": registries,
        "threat_baseline": baseline_decision.as_dict(),
    }


def command_work_order(args: argparse.Namespace) -> dict:
    decision = validate_work_order(
        _read(args.file),
        actor=args.actor,
        default_branch_sha=args.default_branch_sha,
        workflow_sha=args.workflow_sha,
        now=datetime.fromisoformat(args.now.replace("Z", "+00:00")) if args.now else datetime.now(timezone.utc),
        dedup_state=args.dedup_state,
        visibility=args.visibility,
    )
    return decision.as_dict()


def command_review_gate(args: argparse.Namespace) -> dict:
    checks = _read(args.checks)
    expected = set(required_check_names())
    check_green = set(checks) == expected and all(
        item.get("conclusion") == "success" and item.get("head_sha") == args.head_sha
        for item in checks.values()
    )
    decision = validate_review_gate(
        _read(args.request),
        _read(args.review) if Path(args.review).is_file() else None,
        current_head_sha=args.head_sha,
        actual_tree_sha=args.tree_sha,
        required_checks_green=check_green,
        claude_exit_code=args.claude_exit_code,
        base_drift_allowed=not args.base_drift,
        expected_default_branch_sha=args.default_branch_sha,
    )
    return decision.as_dict()


def command_scope(args: argparse.Namespace) -> dict:
    work_order = _read(args.work_order)
    spec = work_order.get("spec", {})
    paths = [
        line.strip()
        for line in Path(args.paths_file).read_text(encoding="utf-8").splitlines()
        if line.strip()
    ]
    return validate_scope(paths, spec.get("scope", []), spec.get("prohibited_paths", [])).as_dict()


def command_manufacturing_result(args: argparse.Namespace) -> dict:
    paths = [
        line.strip()
        for line in Path(args.paths_file).read_text(encoding="utf-8").splitlines()
        if line.strip()
    ]
    return validate_manufacturing_result(
        _read(args.file),
        _read(args.work_order),
        patch_sha256=args.patch_sha256,
        base_sha=args.base_sha,
        changed_paths=paths,
    ).as_dict()


def command_loop(args: argparse.Namespace) -> dict:
    return loop_transition(
        _read(args.review),
        phase=args.phase,
        iteration=args.iteration,
        max_iterations=args.max_iterations,
        previous_patch_hash=args.previous_patch_hash,
        patch_hash=args.patch_hash,
        budget_exceeded=args.budget_exceeded,
    ).as_dict()


def command_route(args: argparse.Namespace) -> dict:
    return route_origin(
        repository=args.repository,
        head_repository=args.head_repository,
        repository_id=args.repository_id,
        head_repository_id=args.head_repository_id,
        branch=args.branch,
    ).as_dict()


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output")
    sub = parser.add_subparsers(dest="command", required=True)

    sub.add_parser("preflight")

    work = sub.add_parser("work-order")
    work.add_argument("--file", required=True)
    work.add_argument("--actor", required=True)
    work.add_argument("--default-branch-sha", required=True)
    work.add_argument("--workflow-sha")
    work.add_argument("--now")
    work.add_argument("--dedup-state", choices=["first", "processed", "indeterminate"], default="first")
    work.add_argument("--visibility", default="public")

    review = sub.add_parser("review-gate")
    review.add_argument("--request", required=True)
    review.add_argument("--review", required=True)
    review.add_argument("--checks", required=True)
    review.add_argument("--head-sha", required=True)
    review.add_argument("--tree-sha", required=True)
    review.add_argument("--claude-exit-code", type=int, default=0)
    review.add_argument("--base-drift", action="store_true")
    review.add_argument("--default-branch-sha")

    scope = sub.add_parser("scope")
    scope.add_argument("--work-order", required=True)
    scope.add_argument("--paths-file", required=True)

    manufactured = sub.add_parser("manufacturing-result")
    manufactured.add_argument("--file", required=True)
    manufactured.add_argument("--work-order", required=True)
    manufactured.add_argument("--patch-sha256", required=True)
    manufactured.add_argument("--base-sha", required=True)
    manufactured.add_argument("--paths-file", required=True)

    loop = sub.add_parser("loop")
    loop.add_argument("--review", required=True)
    loop.add_argument("--phase", choices=["A", "B", "C"], required=True)
    loop.add_argument("--iteration", type=int, required=True)
    loop.add_argument("--max-iterations", type=int, required=True)
    loop.add_argument("--previous-patch-hash")
    loop.add_argument("--patch-hash")
    loop.add_argument("--budget-exceeded", action="store_true")

    route = sub.add_parser("route")
    route.add_argument("--repository", required=True)
    route.add_argument("--head-repository")
    route.add_argument("--repository-id", type=int, required=True)
    route.add_argument("--head-repository-id", type=int)
    route.add_argument("--branch", required=True)
    return parser


def main() -> int:
    parser = build_parser()
    args = parser.parse_args()
    handlers = {
        "preflight": command_preflight,
        "work-order": command_work_order,
        "review-gate": command_review_gate,
        "scope": command_scope,
        "manufacturing-result": command_manufacturing_result,
        "loop": command_loop,
        "route": command_route,
    }
    try:
        result = handlers[args.command](args)
        _write_result(result, args.output)
        return 0 if result.get("accepted", True) else 1
    except ControllerError as exc:
        _write_result({"protocol": "QF-AI-COLLAB-v5", "state": "qf:stopped", "accepted": False, "reasons": [str(exc)]}, args.output)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
