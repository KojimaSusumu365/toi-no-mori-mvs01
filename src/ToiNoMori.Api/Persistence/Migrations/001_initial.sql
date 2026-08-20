CREATE TABLE questions (
    id uuid PRIMARY KEY,
    title varchar(120) NOT NULL,
    body text NOT NULL,
    tags text[] NOT NULL,
    status varchar(16) NOT NULL
        CHECK (status IN ('DRAFT', 'IN_REVIEW', 'PUBLISHED', 'WITHDRAWN')),
    version integer NOT NULL CHECK (version >= 1),
    owner_subject varchar(200) NOT NULL,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    published_at timestamptz NULL,
    review_reason text NULL
);

CREATE INDEX ix_questions_publication
    ON questions (status, published_at DESC, id);

CREATE INDEX ix_questions_tags
    ON questions USING gin (tags);

CREATE TABLE audit_events (
    sequence_id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    id uuid NOT NULL UNIQUE,
    actor_subject varchar(200) NOT NULL,
    target_id uuid NOT NULL,
    action varchar(80) NOT NULL,
    result varchar(160) NOT NULL,
    correlation_id varchar(64) NOT NULL,
    occurred_at timestamptz NOT NULL
);

CREATE INDEX ix_audit_events_target
    ON audit_events (target_id, sequence_id);

CREATE TABLE idempotency_records (
    idempotency_key varchar(128) PRIMARY KEY,
    fingerprint varchar(512) NOT NULL,
    response_snapshot jsonb NOT NULL,
    created_at timestamptz NOT NULL
);
