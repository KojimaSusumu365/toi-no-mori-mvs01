#!/usr/bin/env python3
"""AUTO-T01 through AUTO-T39 (40 deterministic bootstrap cases)."""

from __future__ import annotations

import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
if str(REPO_ROOT) not in sys.path:
    sys.path.insert(0, str(REPO_ROOT))

import copy
import json
import re
import unittest
from datetime import date, datetime, timedelta, timezone

from scripts.ai_controller.core import (
    CONTROL_PLANE_DENY_FILES,
    CONTROL_PLANE_DENYLIST,
    ControllerError,
    GATE_IDS,
    PRECONDITION_IDS,
    ROOT,
    STOP_IDS,
    appointment_applicability,
    canonical_json,
    check_registry_execution,
    content_hash,
    loop_transition,
    load_approved_schema,
    load_registry,
    path_is_denied,
    public_output_is_clean,
    required_check_names,
    review_record_path,
    route_origin,
    schema_validation_errors,
    validate_appointment,
    validate_control_plane,
    validate_disposition_record,
    validate_manufacturing_result,
    validate_patch_identity,
    validate_required_checks,
    validate_review_gate,
    validate_review_record,
    validate_scope,
    validate_schema_definition,
    validate_threat_baseline,
    validate_work_order,
)


HEAD = "a" * 40
TREE = "b" * 40
HASH = "c" * 64


def auto_case(identifier: str):
    def decorate(function):
        function.auto_test_id = identifier
        return function
    return decorate


def valid_work_order() -> dict:
    spec = {
        "objective": "dry run",
        "source_question": "Can bounded automation operate safely?",
        "scope": ["src/"],
        "out_of_scope": ["deployment"],
        "acceptance_criteria": ["all required checks green"],
        "required_tests": list(required_check_names()),
        "prohibited_paths": [".github/"],
        "risk_class": "normal",
        "evidence_required": ["patch_sha256"],
        "stop_conditions": ["scope_exceeded"],
        "rollback_plan": "close Draft PR",
    }
    digest = content_hash(spec)
    return {
        "metadata": {
            "id": "WO-0001",
            "version": 1,
            "source_issue": 1,
            "organizer": "KojimaSusumu365",
            "created_at": "2026-08-28T00:00:00Z",
        },
        "spec": spec,
        "approval": {
            "base_sha": HEAD,
            "work_order_hash": digest,
            "expires_at": "2026-09-04T00:00:00Z",
            "budget": {
                "max_iterations": 0,
                "max_wall_minutes": 60,
                "max_openai_tokens": 1000,
                "max_anthropic_tokens": 1000,
                "max_actions_minutes": 60,
            },
            "organizer_decision": "APPROVED",
            "execution_state": "READY",
            "execution_id": f"WO-0001:1:{digest}",
            "second_human_reviewer": None,
            "second_human_decision": "NOT_REQUIRED",
            "second_human_approved_sha": None,
        },
    }


def valid_request(mode: str = "INITIAL") -> dict:
    return {
        "protocol": "QF-AI-COLLAB-v5",
        "schema_version": "review-request.schema.json@1",
        "request_id": "RR-WO-0001-aaaaaaaa",
        "work_order_id": "WO-0001",
        "pr_number": 7,
        "expected_review_mode": mode,
        "head_sha": HEAD,
        "tree_sha": TREE,
        "prior_review_artifact_sha256": None if mode == "INITIAL" else HASH,
        "eligible_finding_ids": [] if mode == "INITIAL" else ["AUTO-IMPL-P2-001"],
        "fix_candidates": [] if mode == "INITIAL" else [{
            "finding_id": "AUTO-IMPL-P2-001", "candidate_sha": HEAD, "record_sha256": HASH
        }],
    }


def valid_review(request: dict | None = None) -> dict:
    request = request or valid_request()
    return {
        "protocol": "QF-AI-COLLAB-v5",
        "schema_version": "technical-review.schema.json@3",
        "role": "claude_reviewer",
        "review_mode": request["expected_review_mode"],
        "review_request_sha256": content_hash(request),
        "work_order_id": "WO-0001",
        "reviewed_commit_sha": HEAD,
        "reviewed_tree_sha": TREE,
        "default_branch_sha": "d" * 40,
        "decision": "PASS",
        "coverage": {"files_read": ["src/a"], "checks_confirmed": ["tests"], "review_areas": ["security"]},
        "findings": [],
        "notes": [],
        "blocking": False,
        "unverified": [],
    }


def baseline() -> dict:
    return json.loads((ROOT / "docs/governance/threat-model/GITHUB-AUTOMATION.yml").read_text(encoding="utf-8"))


class GitHubAutodriveControllerTests(unittest.TestCase):
    @auto_case("AUTO-T01")
    def test_t01_control_plane_denylist(self):
        executed = set()
        for prefix in CONTROL_PLANE_DENYLIST:
            denied_path = f"{prefix}x"
            self.assertTrue(path_is_denied(denied_path), denied_path)
            self.assertFalse(validate_scope([denied_path], [""], []).accepted)
            executed.add(prefix)
        for denied_file in CONTROL_PLANE_DENY_FILES:
            self.assertTrue(path_is_denied(denied_file), denied_file)
            self.assertFalse(validate_scope([denied_file], [""], []).accepted)
            executed.add(denied_file)
        self.assertEqual(
            set(CONTROL_PLANE_DENYLIST) | CONTROL_PLANE_DENY_FILES,
            executed,
        )
        for invalid in ("", "/etc/passwd", "../x", "a/../b", "C:/x", "a\\b"):
            self.assertTrue(path_is_denied(invalid), invalid)

    @auto_case("AUTO-T02")
    def test_t02_work_order_hash(self):
        work = valid_work_order(); work["approval"]["work_order_hash"] = "0" * 64
        self.assertIn("WO-PRE-009", validate_work_order(work, actor="KojimaSusumu365", default_branch_sha=HEAD, now=datetime(2026, 8, 28, tzinfo=timezone.utc)).reasons)

    @auto_case("AUTO-T03")
    def test_t03_work_order_expiry(self):
        work = valid_work_order(); work["approval"]["expires_at"] = "2026-08-27T00:00:00Z"
        self.assertIn("WO-PRE-010", validate_work_order(work, actor="KojimaSusumu365", default_branch_sha=HEAD, now=datetime(2026, 8, 28, tzinfo=timezone.utc)).reasons)

    @auto_case("AUTO-T04")
    def test_t04_external_fork_origin(self):
        result = route_origin(repository="owner/repo", head_repository="fork/repo", repository_id=1, head_repository_id=2, branch="codex/x")
        self.assertEqual("qf:no-op", result.state); self.assertFalse(result.evidence["artifact_read"])

    @auto_case("AUTO-T05")
    def test_t05_concurrency_namespace(self):
        a = route_origin(repository="o/r", head_repository="o/r", repository_id=1, head_repository_id=1, branch="codex/x")
        b = route_origin(repository="o/r", head_repository="o/r", repository_id=1, head_repository_id=2, branch="codex/x")
        self.assertNotEqual(a.evidence["concurrency_group"], b.evidence["concurrency_group"])

    @auto_case("AUTO-T06")
    def test_t06_job_secret_isolation(self):
        text = (ROOT / ".github/workflows/qf-codex-manufacture.yml").read_text(encoding="utf-8")
        verify = text.split("  verify:", 1)[1].split("  test:", 1)[0]
        product_test = text.split("  test:", 1)[1].split("  publish:", 1)[0]
        self.assertIn("contents: read", verify); self.assertNotIn("OPENAI_API_KEY", verify); self.assertNotIn("write", verify)
        self.assertIn("Seal verified transport before product tests", verify)
        self.assertIn("test-all.sh", product_test); self.assertNotIn("qf-codex-manufactured-identity", product_test)

    @auto_case("AUTO-T07")
    def test_t07_publish_non_execution(self):
        text = (ROOT / ".github/workflows/qf-codex-manufacture.yml").read_text(encoding="utf-8")
        publish = text.split("  publish:", 1)[1]
        for token in ("dotnet", "npm install", "pytest", "test-all.sh"):
            self.assertNotIn(token, publish)
        self.assertLess(publish.index("Verify three-stage patch identity before publisher credential"), publish.index("Mint short-lived"))

    @auto_case("AUTO-T08")
    def test_t08_patch_identity(self):
        self.assertTrue(validate_patch_identity(HASH, HASH, HASH).accepted)
        self.assertFalse(validate_patch_identity(HASH, "d" * 64, HASH).accepted)
        workflow = (ROOT / ".github/workflows/qf-codex-manufacture.yml").read_text(encoding="utf-8")
        self.assertIn("qf-codex-manufactured-identity", workflow)
        self.assertIn("patch-identity --manufactured", workflow)
        work = valid_work_order()
        result = {"protocol":"QF-AI-COLLAB-v5","schema_version":"manufacturing-result.schema.json@3","role":"codex_manufacturer","work_order_id":"WO-0001","work_order_ref":"main:WO-0001.yml","work_order_hash":content_hash(work["spec"]),"base_sha":HEAD,"patch_sha256":HASH,"scope_changed":["src/a"],"tests_requested":list(required_check_names()),"known_limits":[],"iteration":1}
        self.assertTrue(validate_manufacturing_result(result, work, patch_sha256=HASH, base_sha=HEAD, changed_paths=["src/a"]).accepted)
        result["patch_sha256"] = "d" * 64
        self.assertFalse(validate_manufacturing_result(result, work, patch_sha256=HASH, base_sha=HEAD, changed_paths=["src/a"]).accepted)

    @auto_case("AUTO-T09a")
    def test_t09a_required_check_set_identity(self):
        names = required_check_names(); actual = {name: {"conclusion": "success", "head_sha": HEAD} for name in names[:-1]}
        self.assertFalse(validate_required_checks(names, names, actual, HEAD).accepted)
        workflow = (ROOT / ".github/workflows/qf-review-gate.yml").read_text(encoding="utf-8")
        self.assertIn("checks.listForRef", workflow); self.assertNotIn("PLACEHOLDER", workflow)

    @auto_case("AUTO-T09b")
    def test_t09b_required_checks_green(self):
        names = required_check_names(); actual = {name: {"conclusion": "success", "head_sha": HEAD} for name in names}; actual[names[0]]["conclusion"] = "failure"
        self.assertFalse(validate_required_checks(names, names, actual, HEAD).accepted)

    @auto_case("AUTO-T10")
    def test_t10_trusted_prompt_schema(self):
        text = (ROOT / ".github/workflows/qf-codex-manufacture.yml").read_text(encoding="utf-8")
        self.assertIn("$RUNNER_TEMP/qf-control/.github/ai/prompts/codex-manufacture.md", text)
        self.assertIn("output-schema-file: ${{ runner.temp }}/qf-control/.github/ai/schemas/manufacturing-result.schema.json", text)
        self.assertLess(text.index("Move trusted Controller outside the Codex workspace"), text.index("Run Codex manufacturer"))
        for workflow in (ROOT / ".github/workflows").glob("*.yml"):
            for action, ref in re.findall(r"uses:\s+([^\s@]+)@([^\s#]+)", workflow.read_text(encoding="utf-8")):
                self.assertRegex(ref, r"^[0-9a-f]{40}$", f"unpinned action {action}@{ref} in {workflow}")

    @auto_case("AUTO-T11")
    def test_t11_isolated_pr_checkout(self):
        text = (ROOT / ".github/workflows/qf-claude-technical-review.yml").read_text(encoding="utf-8")
        self.assertIn("path: pr-head", text); self.assertIn("--add-dir pr-head", text); self.assertNotIn("pull-requests: write", text)
        self.assertIn("github_token: ${{ secrets.GITHUB_TOKEN }}", text)
        self.assertIn('--allowedTools "Read,Glob,Grep"', text)
        self.assertNotIn("id-token: write", text)
        self.assertIn("qf-review-request-relay", text)
        self.assertNotIn("cp trusted-request/review-request.json technical-review", text)

    @auto_case("AUTO-T12")
    def test_t12_claude_empty_output(self):
        result = validate_review_gate(valid_request(), None, current_head_sha=HEAD, actual_tree_sha=TREE, required_checks_green=True)
        self.assertFalse(result.accepted)

    @auto_case("AUTO-T13")
    def test_t13_review_sha_tree(self):
        review = valid_review(); review["reviewed_tree_sha"] = "e" * 40
        self.assertFalse(validate_review_gate(valid_request(), review, current_head_sha=HEAD, actual_tree_sha=TREE, required_checks_green=True).accepted)

    @auto_case("AUTO-T14")
    def test_t14_stale_head(self):
        self.assertFalse(validate_review_gate(valid_request(), valid_review(), current_head_sha="e" * 40, actual_tree_sha=TREE, required_checks_green=True).accepted)

    @auto_case("AUTO-T15")
    def test_t15_base_drift(self):
        self.assertFalse(validate_review_gate(valid_request(), valid_review(), current_head_sha=HEAD, actual_tree_sha=TREE, required_checks_green=True, base_drift_allowed=False).accepted)

    @auto_case("AUTO-T16")
    def test_t16_public_output_hygiene(self):
        self.assertTrue(public_output_is_clean({"result": "ok"}, ["CANARY_SECRET"])); self.assertFalse(public_output_is_clean({"result": "CANARY_SECRET"}, ["CANARY_SECRET"]))

    @auto_case("AUTO-T17")
    def test_t17_untrusted_actor(self):
        result = validate_work_order(valid_work_order(), actor="outsider", default_branch_sha=HEAD, now=datetime(2026, 8, 28, tzinfo=timezone.utc))
        self.assertIn("WO-PRE-002", result.reasons)

    @auto_case("AUTO-T18")
    def test_t18_budget(self):
        result = loop_transition(valid_review(), phase="B", iteration=2, max_iterations=1, budget_exceeded=True)
        self.assertIn("STOP-020", result.reasons)

    @auto_case("AUTO-T19")
    def test_t19_human_independence(self):
        result = validate_appointment(organizer="KojimaSusumu365", nominee="KojimaSusumu365", head_sha=HEAD, latest_review={"user":"KojimaSusumu365","state":"APPROVED","commit_id":HEAD}, nominee_has_write=False, only_appointment_path_changed=True)
        self.assertFalse(result.accepted)

    @auto_case("AUTO-T20")
    def test_t20_visibility_drift(self):
        value = baseline(); value["repository"]["visibility"] = "private"
        self.assertFalse(validate_threat_baseline(value).accepted)

    @auto_case("AUTO-T21")
    def test_t21_trusted_work_order_trigger(self):
        work = valid_work_order()
        result = validate_work_order(work, actor="KojimaSusumu365", default_branch_sha=HEAD, workflow_sha="f" * 40, now=datetime(2026, 8, 28, tzinfo=timezone.utc))
        self.assertIn("WO-PRE-018", result.reasons)

    @auto_case("AUTO-T22")
    def test_t22_app_privilege_boundary(self):
        value = baseline(); value["github_app"]["permissions"]["workflows"] = "write"
        self.assertFalse(validate_threat_baseline(value).accepted)

    @auto_case("AUTO-T23")
    def test_t23_denylist_false_positive(self):
        self.assertTrue(validate_scope(["src/ToiNoMori.Domain/A.cs"], ["src/"], [".github/"]).accepted)

    @auto_case("AUTO-T24")
    def test_t24_risk_inheritance(self):
        normal = validate_work_order(valid_work_order(), actor="KojimaSusumu365", default_branch_sha=HEAD, now=datetime(2026, 8, 28, tzinfo=timezone.utc))
        work = valid_work_order(); work["spec"]["risk_class"] = "governance"; work["approval"]["work_order_hash"] = content_hash(work["spec"]); work["approval"]["execution_id"] = f"WO-0001:1:{work['approval']['work_order_hash']}"
        governance = validate_work_order(work, actor="KojimaSusumu365", default_branch_sha=HEAD, now=datetime(2026, 8, 28, tzinfo=timezone.utc))
        self.assertTrue(normal.accepted); self.assertIn("WO-PRE-013", governance.reasons)

    @auto_case("AUTO-T25")
    def test_t25_non_convergent_patch(self):
        result = loop_transition(valid_review(), phase="B", iteration=0, max_iterations=1, previous_patch_hash=HASH, patch_hash=HASH)
        self.assertIn("STOP-018", result.reasons)

    @auto_case("AUTO-T26")
    def test_t26_review_schema_contract(self):
        review = valid_review(); review["schema_version"] = "technical-review.schema.json@2"; review["decision"] = "MAYBE"
        self.assertFalse(validate_review_gate(valid_request(), review, current_head_sha=HEAD, actual_tree_sha=TREE, required_checks_green=True).accepted)
        duplicate = valid_review(); duplicate["decision"] = "PASS_WITH_FINDINGS"; duplicate["findings"] = [
            {"id":"AUTO-IMPL-P2-001","severity":"P2","verification_status":"OPEN","disposition":"UNDECIDED","path":"a","evidence":"e","risk":"r","required_change":"c","residual_risk":""},
            {"id":"AUTO-IMPL-P2-001","severity":"P2","verification_status":"OPEN","disposition":"UNDECIDED","path":"b","evidence":"e","risk":"r","required_change":"c","residual_risk":""},
        ]
        self.assertFalse(validate_review_gate(valid_request(), duplicate, current_head_sha=HEAD, actual_tree_sha=TREE, required_checks_green=True).accepted)

    @auto_case("AUTO-T27")
    def test_t27_work_order_organizer(self):
        work = valid_work_order(); work["metadata"]["organizer"] = "outsider"
        self.assertIn("WO-PRE-003", validate_work_order(work, actor="KojimaSusumu365", default_branch_sha=HEAD, now=datetime(2026, 8, 28, tzinfo=timezone.utc)).reasons)

    @auto_case("AUTO-T28")
    def test_t28_reviewer_output_boundary(self):
        review = valid_review(); review["findings"] = [{"id":"AUTO-IMPL-P2-001","severity":"P2","verification_status":"CLOSED","disposition":"ACCEPTED_PLAN"}]
        self.assertFalse(validate_review_gate(valid_request(), review, current_head_sha=HEAD, actual_tree_sha=TREE, required_checks_green=True).accepted)

    @auto_case("AUTO-T29")
    def test_t29_deferral_completeness(self):
        review = valid_review(); review["findings"] = [{"id":"AUTO-IMPL-P2-001"}]
        record = {"decided_by":"KojimaSusumu365","review_artifact_sha256":content_hash(review),"supersedes_record_sha256":None,"decisions":[{"finding_id":"AUTO-IMPL-P2-001","disposition":"DEFERRED","deferral":{"owner":"","reason":"","due":""}}]}
        self.assertFalse(validate_disposition_record(record, review_record=review, now=date(2026,8,28)).accepted)

    @auto_case("AUTO-T30")
    def test_t30_durable_dedup(self):
        result = validate_work_order(valid_work_order(), actor="KojimaSusumu365", default_branch_sha=HEAD, now=datetime(2026, 8, 28, tzinfo=timezone.utc), dedup_state="processed")
        self.assertEqual("qf:no-op", result.state)
        stopped = validate_work_order(valid_work_order(), actor="KojimaSusumu365", default_branch_sha=HEAD, now=datetime(2026, 8, 28, tzinfo=timezone.utc), dedup_state="indeterminate")
        self.assertFalse(stopped.accepted)

    @auto_case("AUTO-T31")
    def test_t31_gate_registry_regression(self):
        executed = {identifier:{"result":"GREEN","evidence":"ok"} for identifier in GATE_IDS[:-1]}
        self.assertFalse(check_registry_execution("gate-checks.yml", GATE_IDS, executed)["accepted"])

    @auto_case("AUTO-T32")
    def test_t32_precondition_registry_regression(self):
        executed = {identifier:{"result":"GREEN","evidence":"ok"} for identifier in PRECONDITION_IDS}; executed["UNKNOWN"]={"result":"GREEN","evidence":"x"}
        self.assertFalse(check_registry_execution("work-order-preconditions.yml", PRECONDITION_IDS, executed)["accepted"])

    @auto_case("AUTO-T33")
    def test_t33_review_mode_integrity(self):
        request = valid_request("REVERIFY"); review = valid_review(request); review["review_mode"] = "INITIAL"; review["findings"]=[{"id":"AUTO-IMPL-P2-002","severity":"P2","verification_status":"VERIFIED","disposition":"UNDECIDED"}]
        self.assertFalse(validate_review_gate(request, review, current_head_sha=HEAD, actual_tree_sha=TREE, required_checks_green=True).accepted)

    @auto_case("AUTO-T34")
    def test_t34_stop_registry_regression(self):
        executed = {identifier:{"result":"GREEN","evidence":"ok"} for identifier in STOP_IDS}; executed.pop(STOP_IDS[0])
        self.assertFalse(check_registry_execution("stop-conditions.yml", STOP_IDS, executed)["accepted"])
        alignment = validate_control_plane()["stop_conditions"]["alignment"]
        self.assertTrue(alignment["accepted"])
        self.assertEqual(sorted(STOP_IDS), alignment["executed"])

    @auto_case("AUTO-T35")
    def test_t35_disposition_record_authority(self):
        review = valid_review(); review["findings"]=[{"id":"AUTO-IMPL-P2-001"}]
        record={"decided_by":"outsider","review_artifact_sha256":content_hash(review),"supersedes_record_sha256":"f"*64,"decisions":[{"finding_id":"AUTO-IMPL-P2-001","disposition":"ACCEPTED_PLAN"}]}
        self.assertFalse(validate_disposition_record(record, review_record=review, existing_record_hashes=[]).accepted)

    @auto_case("AUTO-T36")
    def test_t36_review_result_durability(self):
        review=valid_review(); path,digest=review_record_path(review)
        result=validate_review_record(path,review,exists_on_default=True)
        self.assertTrue(result.accepted); self.assertEqual(digest,result.evidence["sha256"])
        publisher=(ROOT/".github/workflows/qf-review-result-publish.yml").read_text(encoding="utf-8")
        self.assertLess(publisher.index("Canonicalize and verify"),publisher.index("Mint publisher-only token"))
        self.assertIn("canonical/technical-review.json",publisher)
        self.assertIn("state == 'qf:organizer-hold'",publisher)
        self.assertGreaterEqual(publisher.count("if: steps.decision.outputs.should_publish == 'true'"),4)

    @auto_case("AUTO-T37")
    def test_t37_appointment_revocation(self):
        result=validate_appointment(organizer="KojimaSusumu365",nominee="reviewer2",head_sha=HEAD,latest_review={"user":"reviewer2","state":"CHANGES_REQUESTED","commit_id":HEAD},nominee_has_write=False,only_appointment_path_changed=True)
        self.assertFalse(result.accepted)
        value=baseline(); value["automation"]["phase"]="A"
        self.assertFalse(validate_threat_baseline(value).accepted)

    @auto_case("AUTO-T38")
    def test_t38_required_check_applicability(self):
        files=[{"filename":"README.md"}]
        self.assertFalse(appointment_applicability(files,api_success=True,pagination_complete=False).accepted)
        self.assertEqual("not_applicable",appointment_applicability(files,api_success=True,pagination_complete=True).state)
        workflow=(ROOT/".github/workflows/qf-role-appointment-signature.yml").read_text(encoding="utf-8")
        self.assertNotIn("paths-ignore:",workflow); self.assertIn("getCollaboratorPermissionLevel",workflow); self.assertIn("pull_request_review:",workflow)
        self.assertIn("qf-role-appointment-signature", required_check_names())
        registry=load_registry("required-checks.yml")
        provider_workflows={item["workflow_name"] for item in registry["required_checks"]}
        router=(ROOT/".github/workflows/qf-ci-gate-router.yml").read_text(encoding="utf-8")
        for provider in provider_workflows:
            self.assertIn(f'- "{provider}"', router)

    @auto_case("AUTO-T39")
    def test_t39_default_branch_schema_validation(self):
        work = valid_work_order()
        request = valid_request()
        review = valid_review(request)
        manufactured = {
            "protocol": "QF-AI-COLLAB-v5",
            "schema_version": "manufacturing-result.schema.json@3",
            "role": "codex_manufacturer",
            "work_order_id": "WO-0001",
            "work_order_ref": "main:WO-0001.yml",
            "work_order_hash": content_hash(work["spec"]),
            "base_sha": HEAD,
            "patch_sha256": HASH,
            "scope_changed": ["src/a"],
            "tests_requested": list(required_check_names()),
            "known_limits": [],
            "iteration": 1,
        }
        disposition = {
            "protocol": "QF-AI-COLLAB-v5",
            "schema_version": "finding-disposition-record.schema.json@2",
            "role": "organizer",
            "decided_by": "KojimaSusumu365",
            "decided_at": "2026-08-28T00:00:00Z",
            "review_artifact_sha256": HASH,
            "reviewed_commit_sha": HEAD,
            "supersedes_record_sha256": None,
            "decisions": [{
                "finding_id": "AUTO-IMPL-P2-001",
                "disposition": "ACCEPTED_PLAN",
            }],
        }
        valid_documents = (
            (work, "work-order.schema.json"),
            (manufactured, "manufacturing-result.schema.json"),
            (request, "review-request.schema.json"),
            (review, "technical-review.schema.json"),
            (disposition, "finding-disposition-record.schema.json"),
        )
        for document, schema_name in valid_documents:
            self.assertEqual((), schema_validation_errors(document, schema_name))

        invalid_work = copy.deepcopy(work)
        invalid_work["metadata"].pop("id")
        self.assertIn(
            "$.metadata.id:required",
            schema_validation_errors(invalid_work, "work-order.schema.json"),
        )
        self.assertIn(
            "WO-PRE-005",
            validate_work_order(
                invalid_work,
                actor="KojimaSusumu365",
                default_branch_sha=HEAD,
                now=datetime(2026, 8, 28, tzinfo=timezone.utc),
            ).reasons,
        )
        invalid_work = copy.deepcopy(work)
        invalid_work["metadata"]["unexpected"] = True
        self.assertIn(
            "$.metadata.unexpected:additionalProperties",
            schema_validation_errors(invalid_work, "work-order.schema.json"),
        )

        invalid_manufactured = copy.deepcopy(manufactured)
        invalid_manufactured["role"] = "organizer"
        invalid_manufactured["iteration"] = 0
        invalid_manufactured["tests_requested"].append(
            invalid_manufactured["tests_requested"][0]
        )
        manufactured_errors = schema_validation_errors(
            invalid_manufactured, "manufacturing-result.schema.json"
        )
        self.assertIn("$.role:const", manufactured_errors)
        self.assertIn("$.iteration:minimum", manufactured_errors)
        self.assertIn("$.tests_requested:uniqueItems", manufactured_errors)
        self.assertIn(
            "manufacturing-result-json-schema",
            validate_manufacturing_result(
                invalid_manufactured,
                work,
                patch_sha256=HASH,
                base_sha=HEAD,
                changed_paths=["src/a"],
            ).reasons,
        )

        invalid_review = copy.deepcopy(review)
        invalid_review["decision"] = "MAYBE"
        invalid_review["coverage"]["unexpected"] = []
        review_errors = schema_validation_errors(
            invalid_review, "technical-review.schema.json"
        )
        self.assertIn("$.decision:enum", review_errors)
        self.assertIn(
            "$.coverage.unexpected:additionalProperties", review_errors
        )
        self.assertIn(
            "REV-GATE-008",
            validate_review_gate(
                request,
                invalid_review,
                current_head_sha=HEAD,
                actual_tree_sha=TREE,
                required_checks_green=True,
            ).reasons,
        )

        invalid_disposition = copy.deepcopy(disposition)
        invalid_disposition["decided_at"] = "2026-08-28"
        self.assertIn(
            "$.decided_at:format",
            schema_validation_errors(
                invalid_disposition, "finding-disposition-record.schema.json"
            ),
        )
        invalid_disposition["decisions"][0]["disposition"] = []
        self.assertIn(
            "disposition-record-json-schema",
            validate_disposition_record(
                invalid_disposition,
                review_record=review,
                now=date(2026, 8, 28),
            ).reasons,
        )

        unsupported = copy.deepcopy(load_approved_schema("work-order.schema.json"))
        unsupported["oneOf"] = []
        with self.assertRaises(ControllerError):
            validate_schema_definition(unsupported, expected_id="work-order.schema.json@1")
        with self.assertRaises(ControllerError):
            schema_validation_errors({}, "unapproved.schema.json")


if __name__ == "__main__":
    unittest.main()
