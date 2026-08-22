#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
python3 "$SCRIPT_DIR/ci/check-stage6r4c-contract.py"

# Temporary branch-only PLM Fresh source-pool construction. This branch is not intended for merge.
if [[ "${GITHUB_HEAD_REF:-${GITHUB_REF_NAME:-}}" == "plm-fresh-sourcepool-runner-20260823" ]]; then
  python3 "$SCRIPT_DIR/../tools/plm_fresh_sourcepool_build.py"
  mkdir -p "${MVS01_CI_EVIDENCE_DIR:-artifacts/stage6r4c}/plm-fresh-sourcepool"
  cp -a work/out/. "${MVS01_CI_EVIDENCE_DIR:-artifacts/stage6r4c}/plm-fresh-sourcepool/"
fi
