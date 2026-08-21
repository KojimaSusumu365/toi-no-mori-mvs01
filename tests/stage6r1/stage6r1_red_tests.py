#!/usr/bin/env python3
"""Stage 6R executable, implementation-free contracts that remain red.

These checks deliberately describe the approved Stage 6R target while v0.6 is
still present.  ``--assert-red`` succeeds only when every contract is red, so a
test-first baseline can be recorded without pretending the product passed.
As T2 implementation proceeds, run without ``--assert-red`` and replace each
source-level guard with the corresponding behavioural test in its native suite.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import platform
import re
import subprocess
import sys
from dataclasses import asdict, dataclass
from datetime import datetime, timezone
from pathlib import Path
from time import perf_counter
from typing import Callable


ROOT = Path(__file__).resolve().parents[2]


@dataclass(frozen=True)
class Result:
    testId: str
    layer: str
    requirementIds: list[str]
    result: str
    failureCode: str | None
    detail: str
    durationMs: int
    isSimulated: bool = False
    measurementScope: str = "local-static-contract"


def read(relative: str) -> str:
    path = ROOT / relative
    return path.read_text(encoding="utf-8") if path.is_file() else ""


def require(relative: str, *tokens: str) -> tuple[bool, str]:
    content = read(relative)
    if not content:
        return False, f"missing file: {relative}"
    missing = [token for token in tokens if token not in content]
    if missing:
        return False, f"{relative}: missing " + ", ".join(repr(item) for item in missing)
    return True, f"{relative}: contract markers present"


def require_across(relatives: list[str], *tokens: str) -> tuple[bool, str]:
    content = "\n".join(read(relative) for relative in relatives)
    missing = [token for token in tokens if token not in content]
    if missing:
        return False, "missing " + ", ".join(repr(item) for item in missing)
    return True, "contract markers present"


def tc065() -> tuple[bool, str]:
    return require_across(
        ["src/ToiNoMori.Api/AppHost.cs", "src/ToiNoMori.Api/TenantResolver.cs"],
        "external_organization_id",
        "internal_tenant_id",
        "tenant.claim_missing",
        "tenant.claim_invalid_or_unmapped",
    )


def tc066() -> tuple[bool, str]:
    return require_across(
        [
            "src/ToiNoMori.Api/Persistence/Migrations/002_stage6r_expand.sql",
            "src/ToiNoMori.Api/Persistence/Migrations/003_stage6r_contract.sql",
        ],
        "tenant_id uuid",
        "ENABLE ROW LEVEL SECURITY",
        "FORCE ROW LEVEL SECURITY",
        "set_config('app.tenant_id'",
    )


def tc067() -> tuple[bool, str]:
    return require_across(
        [
            "src/ToiNoMori.Api/Persistence/Migrations/002_stage6r_expand.sql",
            "src/ToiNoMori.Api/Persistence/Migrations/003_stage6r_contract.sql",
        ],
        "NULLIF(current_setting('app.tenant_id', true), '')::uuid",
        "questions_tenant_policy",
        "question_revisions_tenant_policy",
        "idempotency_records_tenant_policy",
        "audit_events_tenant_policy",
    )


def tc068() -> tuple[bool, str]:
    return require_across(
        [
            "src/ToiNoMori.Api/Persistence/Migrations/002_stage6r_expand.sql",
            "src/ToiNoMori.Api/Persistence/Migrations/003_stage6r_contract.sql",
        ],
        "uq_revisions_tenant_question_id",
        "fk_revisions_question_same_tenant",
        "fk_published_revision_same_question",
        "FOREIGN KEY (tenant_id, id, published_revision_id)",
    )


def tc069() -> tuple[bool, str]:
    source = read("src/ToiNoMori.Api/ApiEndpointMappings.cs")
    normalized = re.search(
        r'"question\.owner\.forbidden"[^\n]+StatusCodes\.Status404NotFound', source
    )
    if not normalized:
        return False, "owner-forbidden is not normalized to the same 404 contract"
    return True, "visibility failures use the normalized 404 contract"


def tc070() -> tuple[bool, str]:
    return require_across(
        [
            "src/ToiNoMori.Api/AppHost.cs",
            "src/ToiNoMori.Api/CorrelationContextMiddleware.cs",
        ],
        "X-Correlation-ID",
        "X-Request-ID",
        "Guid.NewGuid",
        "request_id",
        "correlation_id",
    )


def tc071_api() -> tuple[bool, str]:
    return require_across(
        [
            "src/ToiNoMori.Api/AppHost.cs",
            "src/ToiNoMori.Api/AccessDenialAuditEnvelope.cs",
        ],
        "AccessDenialAuditEnvelope",
        "access.unauthenticated",
        "csrf.missing_or_invalid",
        "access.rate_limited",
        "resource.not_visible_or_missing",
        "AuditOutcomeRecorded",
        "suppressed",
    )


def tc071_pg() -> tuple[bool, str]:
    return require_across(
        [
            "src/ToiNoMori.Api/Persistence/Migrations/002_stage6r_expand.sql",
            "src/ToiNoMori.Api/Persistence/Migrations/003_stage6r_contract.sql",
        ],
        "platform_security_events",
        "PlatformAuditor",
        "tenant_id uuid NOT NULL",
        "access.unauthenticated",
        "tenant.claim_missing",
        "tenant.claim_invalid_or_unmapped",
    )


def tc073() -> tuple[bool, str]:
    return require_across(
        [
            "src/ToiNoMori.Api/Persistence/Migrations/002_stage6r_expand.sql",
            "src/ToiNoMori.Api/Persistence/Migrations/003_stage6r_contract.sql",
        ],
        "REVOKE UPDATE, DELETE, TRUNCATE",
        "prevent_audit_mutation",
        "prevent_revision_mutation",
        "BEFORE UPDATE OR DELETE",
    )


def tc074() -> tuple[bool, str]:
    return require_across(
        [
            "src/ToiNoMori.Api/Persistence/Migrations/002_stage6r_expand.sql",
            "src/ToiNoMori.Api/Persistence/Migrations/003_stage6r_contract.sql",
        ],
        "idempotency_records",
        "tenant_id",
        "actor_subject",
        "expected_version",
        "expires_at",
    )


def tc075() -> tuple[bool, str]:
    expand = ROOT / "src/ToiNoMori.Api/Persistence/Migrations/002_stage6r_expand.sql"
    contract = ROOT / "src/ToiNoMori.Api/Persistence/Migrations/003_stage6r_contract.sql"
    if not expand.is_file() or not contract.is_file():
        return False, "002 Expand and 003 Contract migrations are both required"
    combined = expand.read_text(encoding="utf-8") + contract.read_text(encoding="utf-8")
    missing = [token for token in ("DROP DEFAULT", "approved_version", "withdrawal_reason", "question_revisions") if token not in combined]
    return (not missing, "missing " + ", ".join(missing) if missing else "expand/contract migration markers present")


def tc076() -> tuple[bool, str]:
    return require(
        "src/ToiNoMori.Api/wwwroot/app/app.js",
        'hasRole("Auditor")',
        "approvalEtag",
        'If-Match',
        "response.status === 409",
        "再読込",
    )


def tc077() -> tuple[bool, str]:
    return require_across(
        [
            "tests/ToiNoMori.OidcE2e.Tests/TestOidcProvider.cs",
            "tests/ToiNoMori.OidcE2e.Tests/Program.cs",
        ],
        "external_organization_id",
        "tenant mapping",
        "TC-ACC-MVS01-077-OIDC",
        'If-Match',
        "self approval",
    )


def tc078() -> tuple[bool, str]:
    return require(
        "scripts/test-disaster-recovery.sh",
        "TC-ACC-MVS01-078-DR",
        "isSimulated",
        "measurementScope",
        "artifactHash",
        "fk_published_revision_same_question",
        "platform_security_events",
    )


def tc080() -> tuple[bool, str]:
    return require_across(
        [
            "src/ToiNoMori.Api/IAuditSink.cs",
            "src/ToiNoMori.Api/AccessDenialAuditEnvelope.cs",
        ],
        "IAuditSink",
        "TimeSpan",
        "audit_write_failures_total",
        "audit_write_duration",
        "fallback",
    )


def tc081_api() -> tuple[bool, str]:
    return require(
        "src/ToiNoMori.Api/Contracts.cs",
        "WithdrawalReason",
        "EditorQuestionResponse",
        "ReviewerQuestionResponse",
        "PublicQuestionResponse",
    )


def tc_perf() -> tuple[bool, str]:
    return require(
        "tests/performance/TC-PERF-MVS01-002-PG.sql",
        "100000",
        "EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON)",
        "Bitmap Index Scan",
        "p95",
        "4000",
        "selection_ratio",
    )


Contract = tuple[str, str, list[str], Callable[[], tuple[bool, str]]]
CONTRACTS: list[Contract] = [
    ("TC-ACC-MVS01-073-PG", "PostgreSQL", ["ADR-0009-D9"], tc073),
    ("TC-ACC-MVS01-076-MOB", "Mobile", ["ADR-0008-D1", "ADR-0009-D7"], tc076),
    ("TC-ACC-MVS01-077-OIDC", "OIDC", ["ADR-0007-D2", "ADR-0008-D1"], tc077),
    ("TC-ACC-MVS01-078-DR", "DR", ["ADR-0007-D5", "ADR-0008-D3"], tc078),
    ("TC-ACC-MVS01-081-API", "API", ["ADR-0008-D4"], tc081_api),
    ("TC-PERF-MVS01-002-PG", "Performance", ["RV-040"], tc_perf),
]


def run() -> list[Result]:
    results: list[Result] = []
    print("# ToiNoMori Stage 6R-6 remaining failure-first contracts")
    print(f"1..{len(CONTRACTS)}")
    for number, (test_id, layer, requirements, check) in enumerate(CONTRACTS, start=1):
        started = perf_counter()
        try:
            passed, detail = check()
            code = None if passed else "STAGE6R_IMPLEMENTATION_MISSING"
        except Exception as exception:  # the harness itself must still emit evidence
            passed = False
            code = "TEST_HARNESS_ERROR"
            detail = f"{type(exception).__name__}: {exception}"
        duration_ms = int((perf_counter() - started) * 1000)
        result = Result(
            testId=test_id,
            layer=layer,
            requirementIds=requirements,
            result="passed" if passed else "failed",
            failureCode=code,
            detail=detail,
            durationMs=duration_ms,
        )
        results.append(result)
        status = "ok" if passed else "not ok"
        print(f"{status} {number} - {test_id} [{','.join(requirements)}] {detail}")
    return results


def tool_version(path: Path, argument: str = "--version") -> str:
    if not path.is_file():
        return "not available"
    completed = subprocess.run(
        [str(path), argument],
        check=False,
        capture_output=True,
        text=True,
        timeout=10,
    )
    output = (completed.stdout or completed.stderr).strip().splitlines()
    return output[0] if completed.returncode == 0 and output else "not executable"


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--assert-red", action="store_true", help="succeed only if all remaining contracts fail for the expected missing-implementation reason")
    parser.add_argument("--json", type=Path, help="write machine-readable evidence")
    args = parser.parse_args()

    started_at = datetime.now(timezone.utc).isoformat()
    results = run()
    failed = sum(item.result == "failed" for item in results)
    harness_errors = sum(item.failureCode == "TEST_HARNESS_ERROR" for item in results)
    source_hash = hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
    evidence = {
        "stage": "6R-6",
        "purpose": "remaining failure-first contracts after platform security audit contracts moved to native API/PostgreSQL suites; failed is expected and is not acceptance",
        "startedAtUtc": started_at,
        "environment": {
            "python": platform.python_version(),
            "dotnet": tool_version(ROOT / ".tools/dotnet/dotnet"),
            "postgresql": tool_version(ROOT / ".tools/postgresql/bin/postgres"),
        },
        "testCount": len(results),
        "failedCount": failed,
        "passedCount": len(results) - failed,
        "harnessErrorCount": harness_errors,
        "evidenceHash": f"sha256:{source_hash}",
        "isSimulated": False,
        "measurementScope": "local-static-contract",
        "results": [asdict(item) for item in results],
    }
    if args.json:
        args.json.parent.mkdir(parents=True, exist_ok=True)
        args.json.write_text(json.dumps(evidence, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    print(f"# result: {len(results) - failed} passed; {failed} failed; {len(results)} total")
    if args.assert_red:
        expected_red = failed == len(results) and harness_errors == 0
        print(f"# failure-first gate: {'EXPECTED RED CONFIRMED' if expected_red else 'INVALID RED BASELINE'}")
        return 0 if expected_red else 1
    return 0 if failed == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
