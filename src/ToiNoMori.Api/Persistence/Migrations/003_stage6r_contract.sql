-- Contract phase: no new row may obtain a tenant implicitly.
ALTER TABLE questions
    ALTER COLUMN tenant_id DROP DEFAULT;

ALTER TABLE audit_events
    ALTER COLUMN tenant_id DROP DEFAULT;

ALTER TABLE idempotency_records
    ALTER COLUMN tenant_id DROP DEFAULT;

-- Application transaction contract:
-- SELECT set_config('app.tenant_id', internal_tenant_id::text, true);
-- The third argument MUST remain true so pooled connections never retain tenant state.

ALTER TABLE questions ENABLE ROW LEVEL SECURITY;
ALTER TABLE questions FORCE ROW LEVEL SECURITY;
CREATE POLICY questions_tenant_policy ON questions
    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

ALTER TABLE question_revisions ENABLE ROW LEVEL SECURITY;
ALTER TABLE question_revisions FORCE ROW LEVEL SECURITY;
CREATE POLICY question_revisions_tenant_policy ON question_revisions
    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

ALTER TABLE idempotency_records ENABLE ROW LEVEL SECURITY;
ALTER TABLE idempotency_records FORCE ROW LEVEL SECURITY;
CREATE POLICY idempotency_records_tenant_policy ON idempotency_records
    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

ALTER TABLE audit_events ENABLE ROW LEVEL SECURITY;
ALTER TABLE audit_events FORCE ROW LEVEL SECURITY;
CREATE POLICY audit_events_tenant_policy ON audit_events
    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
