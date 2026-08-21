-- Stage 6R-7: audit history and question revisions are append-only.
-- Runtime privileges are applied separately by PostgreSqlMigrator because
-- deployment role names are configuration values rather than schema constants.

CREATE FUNCTION prevent_audit_mutation()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION 'append-only audit rows cannot be updated or deleted'
        USING ERRCODE = '55000';
END;
$$;

CREATE TRIGGER prevent_audit_mutation
    BEFORE UPDATE OR DELETE ON audit_events
    FOR EACH ROW
    EXECUTE FUNCTION prevent_audit_mutation();

CREATE TRIGGER prevent_platform_audit_mutation
    BEFORE UPDATE OR DELETE ON platform_security_events
    FOR EACH ROW
    EXECUTE FUNCTION prevent_audit_mutation();

CREATE FUNCTION prevent_revision_mutation()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION 'append-only question revisions cannot be updated or deleted'
        USING ERRCODE = '55000';
END;
$$;

CREATE TRIGGER prevent_revision_mutation
    BEFORE UPDATE OR DELETE ON question_revisions
    FOR EACH ROW
    EXECUTE FUNCTION prevent_revision_mutation();
