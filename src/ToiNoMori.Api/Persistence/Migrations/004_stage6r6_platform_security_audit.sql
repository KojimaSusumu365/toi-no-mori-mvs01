CREATE TABLE platform_security_events (
    id uuid PRIMARY KEY,
    occurred_at timestamptz NOT NULL,
    reason_code varchar(80) NOT NULL CHECK (reason_code IN (
        'access.unauthenticated',
        'access.forbidden',
        'tenant.claim_missing',
        'tenant.claim_invalid_or_unmapped',
        'csrf.missing_or_invalid',
        'access.rate_limited',
        'resource.not_visible_or_missing')),
    normalized_action varchar(200) NOT NULL,
    partition_hash varchar(64) NOT NULL CHECK (length(partition_hash) = 64),
    request_id varchar(64) NOT NULL,
    correlation_id varchar(64) NOT NULL,
    occurrence_count integer NOT NULL DEFAULT 1 CHECK (occurrence_count >= 1),
    window_started_at timestamptz NULL,
    CONSTRAINT ck_platform_rate_window CHECK (
        (reason_code = 'access.rate_limited' AND window_started_at IS NOT NULL)
        OR (reason_code <> 'access.rate_limited' AND window_started_at IS NULL))
);

CREATE INDEX ix_platform_security_events_period
    ON platform_security_events (occurred_at DESC, id DESC);

CREATE UNIQUE INDEX uq_platform_rate_limit_window
    ON platform_security_events (partition_hash, normalized_action, window_started_at)
    WHERE reason_code = 'access.rate_limited';

COMMENT ON TABLE platform_security_events IS
    'Platform-only denial metadata. No tenant, subject, raw IP, token, Cookie, claim, or request body.';
