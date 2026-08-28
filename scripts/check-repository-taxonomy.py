#!/usr/bin/env python3
"""Enforce physical taxonomy, canonical paths, and local Markdown links."""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path
from urllib.parse import unquote

ROOT = Path(__file__).resolve().parents[1]
TAXONOMY = ROOT / "docs/governance/REPOSITORY-TAXONOMY.json"
ERRORS: list[str] = []

required = [
    "README.md",
    "CLAUDE.md",
    "CURRENT_STATE.md",
    "ROADMAP.md",
    "ARCHITECTURE.md",
    "docs/INDEX.md",
    "docs/architecture/README.md",
    "docs/governance/AI-COLLABORATION.md",
    "docs/governance/SOURCE-OF-TRUTH.md",
    "docs/governance/REVIEW-PROTOCOL.md",
    "docs/reviews/stage6r11r/review-request.md",
    "docs/evidence/stage6r10/README.md",
    "docs/evidence/stage6r11/README.md",
    "docs/archive/README.md",
    ".github/PULL_REQUEST_TEMPLATE.md",
]
required += [f"docs/stages/stage6r{number:02d}/README.md" for number in range(1, 13)]
required += [f"docs/evidence/stage6r{number:02d}/README.md" for number in range(1, 12)]

for relative in required:
    if not (ROOT / relative).is_file():
        ERRORS.append(f"required taxonomy file missing: {relative}")

for path in ROOT.glob("MANIFEST-*.md"):
    ERRORS.append(f"root manifest is forbidden: {path.relative_to(ROOT)}")
for pattern in ("adr-*.md", "uml-*.md", "stage6r*.md"):
    for path in (ROOT / "docs").glob(pattern):
        ERRORS.append(f"flat docs path is forbidden: {path.relative_to(ROOT)}")
for path in (ROOT / "docs/evidence").glob("stage6r*.*"):
    ERRORS.append(f"flat evidence path is forbidden: {path.relative_to(ROOT)}")

try:
    taxonomy = json.loads(TAXONOMY.read_text(encoding="utf-8"))
except Exception as exc:
    ERRORS.append(f"taxonomy manifest unreadable: {exc}")
    taxonomy = {"moves": []}

scan_suffixes = {".md", ".py", ".sh", ".yml", ".yaml", ".json"}
scan_excluded = {
    Path("docs/governance/REPOSITORY-TAXONOMY.json"),
    Path("docs/governance/REPOSITORY-TAXONOMY.md"),
}
active_files: list[Path] = []
for path in ROOT.rglob("*"):
    if ".git" in path.parts or not path.is_file() or path.suffix.lower() not in scan_suffixes:
        continue
    relative = path.relative_to(ROOT)
    if relative.parts[:2] in {("docs", "archive"), ("docs", "evidence")}:
        continue
    if relative in scan_excluded:
        continue
    active_files.append(path)

active_text = {
    path: path.read_text(encoding="utf-8", errors="replace")
    for path in active_files
}
for item in taxonomy.get("moves", []):
    old = str(item.get("from", ""))
    # A root Manifest keeps the same basename inside its canonical Stage folder.
    # Structural checks above reject the root file; raw-name scanning would
    # incorrectly reject valid sibling links such as MANIFEST-STAGE6R4-DB.md.
    if not old or "/" not in old:
        continue
    for path, text in active_text.items():
        if old in text:
            ERRORS.append(
                f"legacy path reference {old!r} remains in {path.relative_to(ROOT)}"
            )

link_pattern = re.compile(
    r"!?(?:\[[^\]]*\])\(([^)\s]+)(?:\s+[\"'][^\"']*[\"'])?\)"
)
for path in ROOT.rglob("*.md"):
    if ".git" in path.parts:
        continue
    relative = path.relative_to(ROOT)
    if relative.parts[:2] == ("docs", "archive"):
        continue
    text = path.read_text(encoding="utf-8", errors="replace")
    for match in link_pattern.finditer(text):
        target = match.group(1).strip("<>")
        if (
            not target
            or target.startswith("#")
            or re.match(r"^[a-z][a-z0-9+.-]*:", target, re.I)
        ):
            continue
        bare = unquote(re.split(r"[?#]", target, maxsplit=1)[0])
        if not bare:
            continue
        candidate = (
            ROOT / bare.lstrip("/")
            if bare.startswith("/")
            else path.parent / bare
        ).resolve()
        try:
            candidate.relative_to(ROOT.resolve())
        except ValueError:
            ERRORS.append(f"link escapes repository: {relative} -> {target}")
            continue
        if not candidate.exists():
            ERRORS.append(f"broken local link: {relative} -> {target}")

if ERRORS:
    print(
        "Repository taxonomy contract failed:",
        *sorted(set(ERRORS)),
        sep="\n- ",
        file=sys.stderr,
    )
    raise SystemExit(1)

print(
    f"Repository taxonomy contract OK: {len(required)} required files; "
    "local links valid"
)
