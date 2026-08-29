-- Expand phase: existing Stage 6 rows are assigned to the approved migration tenant.
-- The defaults are temporary compatibility aids and are removed by 003 Contract.
CREATE TABLE tenants (
    id uuid PRIMARY KEY,
    tenant_code varchar(80) NOT NULL UNIQUE,
    display_name varchar(200) NOT NULL,
    is_active boolean NOT NULL,
    created_at timestamptz NOT NULL
);

INSERT INTO tenants (id, tenant_code, display_name, is_active, created_at)
VALUES (
    '7b48e239-07ef-4b34-a1fb-7f4fc7ff1673',
    'mvs01',
    'MVS-01 migration tenant',
    true,
    now());

ALTER TABLE questions
    ADD COLUMN tenant_id uuid DEFAULT '7b48e239-07ef-4b34-a1fb-7f4fc7ff1673',
    ADD COLUMN withdrawal_reason text NULL,
    ADD COLUMN approved_version integer NULL,
    ADD COLUMN approved_by varchar(200) NULL,
    ADD COLUMN published_revision_id uuid NULL;

UPDATE questions
SET tenant_id = '7b48e239-07ef-4b34-a1fb-7f4fc7ff1673'
WHERE tenant_id IS NULL;

ALTER TABLE questions
    ALTER COLUMN tenant_id SET NOT NULL,
    ADD CONSTRAINT uq_questions_tenant_id UNIQUE (tenant_id, id),
    ADD CONSTRAINT fk_questions_tenant
        FOREIGN KEY (tenant_id) REFERENCES tenants (id),
    ADD CONSTRAINT ck_questions_approved_version
        CHECK (approved_version IS NULL OR approved_version >= 1);

ALTER TABLE audit_events
    ADD COLUMN tenant_id uuid DEFAULT '7b48e239-07ef-4b34-a1fb-7f4fc7ff1673';

UPDATE audit_events
SET tenant_id = '7b48e239-07ef-4b34-a1fb-7f4fc7ff1673'
WHERE tenant_id IS NULL;

ALTER TABLE audit_events
    ALTER COLUMN tenant_id SET NOT NULL,
    ADD CONSTRAINT fk_audit_events_tenant
        FOREIGN KEY (tenant_id) REFERENCES tenants (id);

ALTER TABLE idempotency_records
    ADD COLUMN tenant_id uuid DEFAULT '7b48e239-07ef-4b34-a1fb-7f4fc7ff1673',
    ADD COLUMN actor_subject varchar(200) NULL,
    ADD COLUMN expected_version integer NULL,
    ADD COLUMN expires_at timestamptz NULL;

UPDATE idempotency_records
SET tenant_id = '7b48e239-07ef-4b34-a1fb-7f4fc7ff1673',
    actor_subject = COALESCE(NULLIF(response_snapshot ->> 'approvedBy', ''), 'migrated:unknown'),
    expected_version = COALESCE((response_snapshot ->> 'approvedVersion')::integer, 1),
    expires_at = created_at + interval '24 hours'
WHERE tenant_id IS NULL
   OR actor_subject IS NULL
   OR expected_version IS NULL
   OR expires_at IS NULL;

ALTER TABLE idempotency_records
    ALTER COLUMN tenant_id SET NOT NULL,
    ALTER COLUMN actor_subject SET NOT NULL,
    ALTER COLUMN expected_version SET NOT NULL,
    ALTER COLUMN expires_at SET NOT NULL,
    DROP CONSTRAINT idempotency_records_pkey,
    ADD CONSTRAINT idempotency_records_pkey PRIMARY KEY (tenant_id, idempotency_key),
    ADD CONSTRAINT fk_idempotency_records_tenant
        FOREIGN KEY (tenant_id) REFERENCES tenants (id),
    ADD CONSTRAINT ck_idempotency_expected_version CHECK (expected_version >= 1);

CREATE TABLE question_revisions (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    question_id uuid NOT NULL,
    version integer NOT NULL CHECK (version >= 1),
    title varchar(120) NOT NULL,
    body text NOT NULL,
    tags text[] NOT NULL,
    status varchar(16) NOT NULL
        CHECK (status IN ('DRAFT', 'IN_REVIEW', 'PUBLISHED', 'WITHDRAWN')),
    owner_subject varchar(200) NOT NULL,
    created_at timestamptz NOT NULL,
    recorded_at timestamptz NOT NULL,
    published_at timestamptz NULL,
    review_reason text NULL,
    withdrawal_reason text NULL,
    approved_version integer NULL CHECK (approved_version IS NULL OR approved_version >= 1),
    approved_by varchar(200) NULL,
    CONSTRAINT pk_question_revisions PRIMARY KEY (tenant_id, id),
    CONSTRAINT uq_revisions_tenant_question_id UNIQUE (tenant_id, question_id, id),
    CONSTRAINT uq_revisions_tenant_question_version UNIQUE (tenant_id, question_id, version),
    CONSTRAINT fk_revisions_question_same_tenant
        FOREIGN KEY (tenant_id, question_id)
        REFERENCES questions (tenant_id, id)
        DEFERRABLE INITIALLY DEFERRED
);

-- Build this index before the backfill. The deferred same-tenant FK creates
-- pending trigger events for inserted revisions, and PostgreSQL rejects an
-- index build on that table until those events have been resolved at commit.
CREATE INDEX ix_revisions_tenant_question_version
    ON question_revisions (tenant_id, question_id, version DESC);

INSERT INTO question_revisions (
    tenant_id, id, question_id, version, title, body, tags, status,
    owner_subject, created_at, recorded_at, published_at, review_reason,
    withdrawal_reason, approved_version, approved_by)
SELECT
    tenant_id, id, id, version, title, body, tags, status,
    owner_subject, created_at, updated_at, published_at, review_reason,
    withdrawal_reason,
    CASE WHEN status IN ('PUBLISHED', 'WITHDRAWN') THEN version ELSE approved_version END,
    CASE WHEN status IN ('PUBLISHED', 'WITHDRAWN') THEN 'migrated:unknown' ELSE approved_by END
FROM questions;

UPDATE questions
SET approved_version = version,
    approved_by = COALESCE(approved_by, 'migrated:unknown'),
    published_revision_id = id
WHERE status IN ('PUBLISHED', 'WITHDRAWN');

ALTER TABLE questions
    ADD CONSTRAINT fk_published_revision_same_question
        FOREIGN KEY (tenant_id, id, published_revision_id)
        REFERENCES question_revisions (tenant_id, question_id, id)
        DEFERRABLE INITIALLY DEFERRED;

CREATE INDEX ix_questions_tenant_publication
    ON questions (tenant_id, status, published_at DESC, id);

CREATE INDEX ix_questions_tenant_owner_updated
    ON questions (tenant_id, owner_subject, updated_at DESC, id);

CREATE INDEX ix_audit_events_tenant_sequence
    ON audit_events (tenant_id, sequence_id);

CREATE INDEX ix_idempotency_records_expiry
    ON idempotency_records (tenant_id, expires_at);
