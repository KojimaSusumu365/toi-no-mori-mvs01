#!/usr/bin/env python3
"""Fail when the repository's human/AI onboarding contract becomes incomplete."""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
REQUIRED = [
    "README.md",
    "CLAUDE.md",
    "CURRENT_STATE.md",
    "ARCHITECTURE.md",
    "ROADMAP.md",
    "docs/reviews/stage6r11r/MANIFEST.md",
    "docs/INDEX.md",
    "docs/architecture/README.md",
    "docs/stages/README.md",
    "docs/stages/stage6r11/MANIFEST-STAGE6R11.md",
    "docs/stages/stage6r12/README.md",
    "docs/evidence/README.md",
    "docs/evidence/stage6r11/stage6r11r-github-acceptance.md",
    "docs/archive/README.md",
    "docs/governance/REPOSITORY-TAXONOMY.md",
    "docs/governance/REPOSITORY-TAXONOMY.json",
    "docs/architecture/contracts/forest-town-boundary-v1.md",
    "docs/reviews/stage6r11r/closure.md",
    "docs/reviews/stage6r11r/test-id-mapping.md",
    "spec/deferred-tests.json",
    "docs/governance/AI-COLLABORATION.md",
    "docs/governance/SOURCE-OF-TRUTH.md",
    "docs/governance/REVIEW-PROTOCOL.md",
    "docs/reviews/README.md",
    "docs/reviews/schema/review-manifest.schema.json",
    "docs/reviews/stage6r11r/review-request.md",
    "docs/reviews/stage6r11r/review-manifest.json",
    "docs/reviews/stage6r11r/claude-findings.md",
    "docs/reviews/stage6r11r/codex-response.md",
    "docs/reviews/stage6r11r/final-acceptance.md",
    "docs/reviews/stage6r11r/closure.md",
    "docs/reviews/stage6r11r/test-id-mapping.md",
]
missing = [path for path in REQUIRED if not (ROOT / path).is_file()]
if missing:
    print("Missing repository navigation files:", *missing, sep="\n- ", file=sys.stderr)
    raise SystemExit(1)

manifest_path = ROOT / "docs/reviews/stage6r11r/review-manifest.json"
manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
sha_pattern = re.compile(r"^[0-9a-f]{40}$")
errors: list[str] = []

for field in ("stage", "status", "repository", "base_branch", "reviewer", "findings", "final_acceptance"):
    if field not in manifest:
        errors.append(f"manifest missing {field}")

target = manifest.get("review_target", {})
for field in ("commit_sha", "tree_sha"):
    if not sha_pattern.fullmatch(str(target.get(field, ""))):
        errors.append(f"review_target.{field} must be a 40-character lowercase SHA")

if manifest.get("status") == "CLOSED" and manifest.get("final_acceptance") != "ACCEPTED":
    errors.append("CLOSED requires final_acceptance=ACCEPTED")

claude_text = (ROOT / "CLAUDE.md").read_text(encoding="utf-8")
for link in ("CURRENT_STATE.md", "SOURCE-OF-TRUTH.md", "REVIEW-PROTOCOL.md"):
    if link not in claude_text:
        errors.append(f"CLAUDE.md must link to {link}")

if errors:
    print("Repository navigation contract failed:", *errors, sep="\n- ", file=sys.stderr)
    raise SystemExit(1)

print(f"Repository navigation contract OK: {len(REQUIRED)} required files")
