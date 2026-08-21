using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Npgsql;
using ToiNoMori.Api;
using ToiNoMori.Api.Persistence;
using ToiNoMori.Domain;
using ToiNoMori.PostgreSql.Tests;
using ToiNoMori.Testing;

var connectionString = Environment.GetEnvironmentVariable("MVS01_TEST_POSTGRES_CONNECTION")
    ?? throw new InvalidOperationException("MVS01_TEST_POSTGRES_CONNECTION is required.");
var migrationConnectionString = Environment.GetEnvironmentVariable(
        "MVS01_TEST_POSTGRES_MIGRATOR_CONNECTION")
    ?? throw new InvalidOperationException(
        "MVS01_TEST_POSTGRES_MIGRATOR_CONNECTION is required.");
var administrationConnectionString = Environment.GetEnvironmentVariable(
        "MVS01_TEST_POSTGRES_ADMIN_CONNECTION")
    ?? throw new InvalidOperationException(
        "MVS01_TEST_POSTGRES_ADMIN_CONNECTION is required.");
var bypassConnectionString = Environment.GetEnvironmentVariable(
        "MVS01_TEST_POSTGRES_BYPASS_CONNECTION")
    ?? throw new InvalidOperationException(
        "MVS01_TEST_POSTGRES_BYPASS_CONNECTION is required.");
var platformAuditWriterConnectionString = Environment.GetEnvironmentVariable(
        "MVS01_TEST_POSTGRES_PLATFORM_AUDIT_WRITER_CONNECTION")
    ?? throw new InvalidOperationException(
        "MVS01_TEST_POSTGRES_PLATFORM_AUDIT_WRITER_CONNECTION is required.");
var platformAuditReaderConnectionString = Environment.GetEnvironmentVariable(
        "MVS01_TEST_POSTGRES_PLATFORM_AUDIT_READER_CONNECTION")
    ?? throw new InvalidOperationException(
        "MVS01_TEST_POSTGRES_PLATFORM_AUDIT_READER_CONNECTION is required.");

var tests = new List<SpecTest>
{
    new("TC-ACC-MVS01-066-PG", "ADR-0007-D1,ADR-0007-D3", "tenant列・強制RLS・transaction-local設定", async () =>
    {
        await using var fixture = await PostgreSqlApiFixture.StartAsync(
            connectionString,
            migrationConnectionString);
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var connection = await dataSource.OpenConnectionAsync();

        await using (var catalog = new NpgsqlCommand(
            """
            SELECT count(*)
            FROM pg_class AS table_class
            JOIN pg_namespace AS table_namespace
              ON table_namespace.oid = table_class.relnamespace
            WHERE table_namespace.nspname = current_schema()
              AND table_class.relname = ANY (
                  ARRAY['questions', 'question_revisions', 'idempotency_records', 'audit_events'])
              AND table_class.relrowsecurity
              AND table_class.relforcerowsecurity;
            """,
            connection))
        {
            var protectedTableCount = (long)(await catalog.ExecuteScalarAsync() ?? 0L);
            SpecAssert.Equal(4L, protectedTableCount, "All four tenant business tables must enable and force RLS.");
        }

        await using (var roleBoundary = new NpgsqlCommand(
            """
            SELECT NOT role.rolsuper
               AND NOT role.rolinherit
               AND NOT role.rolbypassrls
               AND NOT EXISTS (
                   SELECT 1
                   FROM pg_class AS table_class
                   JOIN pg_namespace AS table_namespace
                     ON table_namespace.oid = table_class.relnamespace
                   WHERE table_namespace.nspname = current_schema()
                     AND table_class.relname = ANY (
                       ARRAY['questions', 'question_revisions', 'idempotency_records', 'audit_events'])
                     AND table_class.relowner = role.oid)
            FROM pg_roles AS role
            WHERE role.rolname = current_user;
            """,
            connection))
        {
            var isRestrictedApplicationRole = (bool)(await roleBoundary.ExecuteScalarAsync() ?? false);
            SpecAssert.True(
                isRestrictedApplicationRole,
                "The application role must be NOINHERIT, non-superuser, non-BYPASSRLS, and not own tenant tables.");
        }

        await using (var leastPrivilege = new NpgsqlCommand(
            """
            SELECT NOT has_schema_privilege(current_user, current_schema(), 'CREATE')
               AND has_schema_privilege(current_user, current_schema(), 'USAGE')
               AND has_table_privilege(current_user, 'questions', 'SELECT')
               AND has_table_privilege(current_user, 'questions', 'INSERT')
               AND has_table_privilege(current_user, 'questions', 'UPDATE')
               AND NOT has_table_privilege(current_user, 'questions', 'DELETE')
               AND NOT has_table_privilege(current_user, 'questions', 'TRUNCATE')
               AND has_table_privilege(current_user, 'question_revisions', 'INSERT')
               AND NOT has_table_privilege(current_user, 'question_revisions', 'UPDATE')
               AND has_table_privilege(current_user, 'idempotency_records', 'DELETE')
               AND NOT has_table_privilege(current_user, 'idempotency_records', 'UPDATE')
               AND has_table_privilege(current_user, 'audit_events', 'INSERT')
               AND NOT has_table_privilege(current_user, 'audit_events', 'UPDATE')
               AND NOT has_table_privilege(current_user, 'audit_events', 'DELETE')
               AND NOT has_table_privilege(current_user, 'schema_migrations', 'SELECT');
            """,
            connection))
        {
            var hasLeastPrivilege = (bool)(await leastPrivilege.ExecuteScalarAsync() ?? false);
            SpecAssert.True(
                hasLeastPrivilege,
                "The application role must have tenant DML only and no schema or migration-ledger authority.");
        }

        await using (var migrationDataSource = NpgsqlDataSource.Create(migrationConnectionString))
        await using (var migrationConnection = await migrationDataSource.OpenConnectionAsync())
        await using (var migrationOwnership = new NpgsqlCommand(
            """
            SELECT current_user <> @application_role
               AND count(*) = 4
               AND bool_and(pg_get_userbyid(table_class.relowner) = current_user)
            FROM pg_class AS table_class
            JOIN pg_namespace AS table_namespace
              ON table_namespace.oid = table_class.relnamespace
            WHERE table_namespace.nspname = current_schema()
              AND table_class.relname = ANY (
                  ARRAY['questions', 'question_revisions', 'idempotency_records', 'audit_events']);
            """,
            migrationConnection))
        {
            migrationOwnership.Parameters.AddWithValue(
                "application_role",
                new NpgsqlConnectionStringBuilder(connectionString).Username
                    ?? throw new TestFailureException(
                        "The application test connection must contain an explicit username."));
            var ownedBySeparateMigrationRole =
                (bool)(await migrationOwnership.ExecuteScalarAsync() ?? false);
            SpecAssert.True(
                ownedBySeparateMigrationRole,
                "The protected tables must be owned by a separate migration role.");
        }

        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await SetTenantAsync(connection, transaction, TenantIds.Mvs01);
            await using var current = new NpgsqlCommand(
                "SELECT current_setting('app.tenant_id', true);",
                connection,
                transaction);
            var inside = (string?)await current.ExecuteScalarAsync();
            SpecAssert.Equal(TenantIds.Mvs01.ToString("D"), inside, "The transaction must carry the internal tenant UUID.");
            await transaction.CommitAsync();
        }

        await using (var after = new NpgsqlCommand(
            "SELECT current_setting('app.tenant_id', true);",
            connection))
        {
            var outside = await after.ExecuteScalarAsync();
            SpecAssert.True(
                outside is null or DBNull || string.IsNullOrEmpty((string)outside),
                "Transaction-local tenant state must be cleared before the pooled connection is reused.");
        }

        await AssertRoleBoundaryRejectedAsync(
            administrationConnectionString,
            "A superuser application credential must be rejected at startup.");
        await AssertRoleBoundaryRejectedAsync(
            migrationConnectionString,
            "A table-owner migration credential must be rejected as an application credential.");
        await AssertRoleBoundaryRejectedAsync(
            bypassConnectionString,
            "A BYPASSRLS application credential must be rejected at startup.");
    }),
    new("TC-ACC-MVS01-071-PG", "ADR-0010-D1,RVR-N01", "platform監査表とwriter/readerロールをtenant境界から分離", async () =>
    {
        await using var fixture = await PostgreSqlApiFixture.StartAsync(
            connectionString,
            migrationConnectionString,
            platformAuditWriterConnectionString,
            platformAuditReaderConnectionString);
        var applicationRole = new NpgsqlConnectionStringBuilder(connectionString).Username
            ?? throw new TestFailureException("The application test role is required.");
        var writerRole = new NpgsqlConnectionStringBuilder(platformAuditWriterConnectionString).Username
            ?? throw new TestFailureException("The platform writer test role is required.");
        var readerRole = new NpgsqlConnectionStringBuilder(platformAuditReaderConnectionString).Username
            ?? throw new TestFailureException("The platform reader test role is required.");

        await using var migrationDataSource = NpgsqlDataSource.Create(migrationConnectionString);
        await using var migrationConnection = await migrationDataSource.OpenConnectionAsync();
        await using (var boundary = new NpgsqlCommand(
            """
            SELECT
                NOT has_table_privilege(@application_role, 'platform_security_events', 'SELECT,INSERT,UPDATE,DELETE,TRUNCATE')
                AND has_table_privilege(@writer_role, 'platform_security_events', 'INSERT')
                AND NOT has_table_privilege(@writer_role, 'platform_security_events', 'SELECT,UPDATE,DELETE,TRUNCATE')
                AND has_table_privilege(@reader_role, 'platform_security_events', 'SELECT')
                AND NOT has_table_privilege(@reader_role, 'platform_security_events', 'INSERT,UPDATE,DELETE,TRUNCATE')
                AND NOT EXISTS (
                    SELECT 1 FROM pg_roles
                    WHERE rolname IN (@writer_role, @reader_role)
                      AND (rolsuper OR rolinherit OR rolbypassrls))
                AND NOT EXISTS (
                    SELECT 1
                    FROM information_schema.columns
                    WHERE table_schema = current_schema()
                      AND table_name = 'platform_security_events'
                      AND column_name IN ('tenant_id', 'subject', 'raw_ip', 'token', 'cookie', 'claim', 'body'));
            """,
            migrationConnection))
        {
            boundary.Parameters.AddWithValue("application_role", applicationRole);
            boundary.Parameters.AddWithValue("writer_role", writerRole);
            boundary.Parameters.AddWithValue("reader_role", readerRole);
            var separated = (bool)(await boundary.ExecuteScalarAsync() ?? false);
            SpecAssert.True(separated, "Application, platform writer, and PlatformAuditor reader must have non-overlapping least privileges.");
        }

        var eventId = Guid.NewGuid();
        await using (var writerDataSource = NpgsqlDataSource.Create(platformAuditWriterConnectionString))
        await using (var writerConnection = await writerDataSource.OpenConnectionAsync())
        {
            await using var valid = new NpgsqlCommand(
                """
                INSERT INTO platform_security_events (
                    id, occurred_at, reason_code, normalized_action, partition_hash,
                    request_id, correlation_id, occurrence_count, window_started_at)
                VALUES (
                    @id, now(), 'tenant.claim_missing', 'POST /api/admin/questions',
                    repeat('a', 64), 'request-test', 'correlation-test', 1, NULL);
                """,
                writerConnection);
            valid.Parameters.AddWithValue("id", eventId);
            SpecAssert.Equal(1, await valid.ExecuteNonQueryAsync(), "The dedicated writer must append an allowlisted platform event.");

            await using var invalid = new NpgsqlCommand(
                """
                INSERT INTO platform_security_events (
                    id, occurred_at, reason_code, normalized_action, partition_hash,
                    request_id, correlation_id, occurrence_count, window_started_at)
                VALUES (
                    @id, now(), 'claim.raw.secret', 'POST /protected',
                    repeat('b', 64), 'request-invalid', 'correlation-invalid', 1, NULL);
                """,
                writerConnection);
            invalid.Parameters.AddWithValue("id", Guid.NewGuid());
            try
            {
                await invalid.ExecuteNonQueryAsync();
                throw new TestFailureException("The database must reject a non-allowlisted reason code.");
            }
            catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.CheckViolation)
            {
            }
        }

        await using var readerDataSource = NpgsqlDataSource.Create(platformAuditReaderConnectionString);
        await using var readerConnection = await readerDataSource.OpenConnectionAsync();
        await using var read = new NpgsqlCommand(
            "SELECT count(*) FROM platform_security_events WHERE id = @id AND reason_code = 'tenant.claim_missing';",
            readerConnection);
        read.Parameters.AddWithValue("id", eventId);
        SpecAssert.Equal(1L, (long)(await read.ExecuteScalarAsync() ?? 0L), "The reader-only PlatformAuditor credential must read the append-only projection.");
    }),
    new("TC-ACC-MVS01-067-PG", "ADR-0007-D3,RVA-C06", "RLSで他tenant行を不可視化", async () =>
    {
        await using var fixture = await PostgreSqlApiFixture.StartAsync(
            connectionString,
            migrationConnectionString);
        await EnsureTenantAsync(
            administrationConnectionString,
            Guid.Parse("a46f716d-6f13-4e98-a7e0-a04228ba0a90"),
            "stage6r4-test-other");
        const string sharedSubject = "rls-shared-subject";
        using var tenantAEditor = fixture.AuthenticatedClient(sharedSubject, "Editor", "org-mvs01");
        using var createdResponse = await tenantAEditor.PostAsJsonAsync(
            "/api/admin/questions",
            ValidContent("rls isolation"));
        var created = await ReadQuestionAsync(createdResponse);

        using var tenantBEditor = fixture.AuthenticatedClient(sharedSubject, "Editor", "org-other");
        using var tenantBCreate = await tenantBEditor.PostAsJsonAsync(
            "/api/admin/questions",
            ValidContent("rls tenant b"));
        SpecAssert.Equal(HttpStatusCode.Created, tenantBCreate.StatusCode, "The same subject may own a distinct Tenant B row.");
        using var tenantBReviewer = fixture.AuthenticatedClient(sharedSubject, "Reviewer", "org-other");
        using var hidden = await tenantBReviewer.GetAsync($"/api/admin/questions/{created.Id}");
        SpecAssert.Equal(HttpStatusCode.NotFound, hidden.StatusCode, "Another tenant must receive normalized 404.");

        var pooledConnectionString = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Pooling = true,
            MaxPoolSize = 1
        }.ConnectionString;
        await using var dataSource = NpgsqlDataSource.Create(pooledConnectionString);
        var visibleToA = await CountQuestionAsync(dataSource, TenantIds.Mvs01, created.Id);
        var visibleToB = await CountQuestionAsync(
            dataSource,
            Guid.Parse("a46f716d-6f13-4e98-a7e0-a04228ba0a90"),
            created.Id);
        SpecAssert.Equal(1L, visibleToA, "The owning tenant must see its row through RLS.");
        SpecAssert.Equal(0L, visibleToB, "A different tenant setting must see zero rows.");

        var visibleWithEmptySetting = await CountQuestionWithRawSettingAsync(
            dataSource,
            string.Empty,
            created.Id);
        SpecAssert.Equal(0L, visibleWithEmptySetting, "An empty pooled custom setting must safely return zero rows.");

        var visibleAfterPoolReuse = await CountQuestionWithoutSettingAsync(dataSource, created.Id);
        SpecAssert.Equal(
            0L,
            visibleAfterPoolReuse,
            "Reusing the one-connection pool without a tenant setting must not retain Tenant A visibility.");
    }),
    new("TC-ACC-MVS01-068-PG", "ADR-0007-D4,RVA-C05", "同一tenant・同一question複合外部キー", async () =>
    {
        await using var fixture = await PostgreSqlApiFixture.StartAsync(
            connectionString,
            migrationConnectionString);
        await EnsureTenantAsync(
            administrationConnectionString,
            Guid.Parse("a46f716d-6f13-4e98-a7e0-a04228ba0a90"),
            "stage6r4-test-other");
        using var editor = fixture.AuthenticatedClient("fk-owner", "Editor", "org-mvs01");
        using var firstResponse = await editor.PostAsJsonAsync("/api/admin/questions", ValidContent("fk first"));
        var first = await ReadQuestionAsync(firstResponse);
        using var secondResponse = await editor.PostAsJsonAsync("/api/admin/questions", ValidContent("fk second"));
        var second = await ReadQuestionAsync(secondResponse);

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var connection = await dataSource.OpenConnectionAsync();
        Guid secondRevisionId;
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await SetTenantAsync(connection, transaction, TenantIds.Mvs01);
            await using var findRevision = new NpgsqlCommand(
                """
                SELECT id
                FROM question_revisions
                WHERE tenant_id = @tenant_id AND question_id = @question_id AND version = 1;
                """,
                connection,
                transaction);
            findRevision.Parameters.AddWithValue("tenant_id", TenantIds.Mvs01);
            findRevision.Parameters.AddWithValue("question_id", second.Id);
            secondRevisionId = (Guid)(await findRevision.ExecuteScalarAsync()
                ?? throw new TestFailureException("The second question revision was not created."));
            await transaction.CommitAsync();
        }

        var sameTenantWrongQuestionRejected = false;
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await SetTenantAsync(connection, transaction, TenantIds.Mvs01);
            await using var update = new NpgsqlCommand(
                """
                UPDATE questions
                SET published_revision_id = @revision_id
                WHERE tenant_id = @tenant_id AND id = @question_id;
                """,
                connection,
                transaction);
            update.Parameters.AddWithValue("revision_id", secondRevisionId);
            update.Parameters.AddWithValue("tenant_id", TenantIds.Mvs01);
            update.Parameters.AddWithValue("question_id", first.Id);
            await update.ExecuteNonQueryAsync();
            try
            {
                await transaction.CommitAsync();
            }
            catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.ForeignKeyViolation)
            {
                sameTenantWrongQuestionRejected = true;
            }
        }
        SpecAssert.True(
            sameTenantWrongQuestionRejected,
            "A revision belonging to another question must fail the composite foreign key.");

        var crossTenantRevisionRejected = false;
        var tenantB = Guid.Parse("a46f716d-6f13-4e98-a7e0-a04228ba0a90");
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await SetTenantAsync(connection, transaction, tenantB);
            await using var insert = new NpgsqlCommand(
                """
                INSERT INTO question_revisions (
                    tenant_id, id, question_id, version, title, body, tags, status,
                    owner_subject, created_at, recorded_at)
                VALUES (
                    @tenant_id, @id, @question_id, 99, 'cross tenant', 'cross tenant',
                    ARRAY[]::text[], 'DRAFT', 'fk-attacker', now(), now());
                """,
                connection,
                transaction);
            insert.Parameters.AddWithValue("tenant_id", tenantB);
            insert.Parameters.AddWithValue("id", Guid.NewGuid());
            insert.Parameters.AddWithValue("question_id", first.Id);
            await insert.ExecuteNonQueryAsync();
            try
            {
                await transaction.CommitAsync();
            }
            catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.ForeignKeyViolation)
            {
                crossTenantRevisionRejected = true;
            }
        }
        SpecAssert.True(
            crossTenantRevisionRejected,
            "A cross-tenant revision must fail the same-tenant composite foreign key.");
    }),
    new("TC-ACC-MVS01-074-PG", "ADR-0008-D5,RV-023", "冪等キーをtenant・actor・対象版で分離", async () =>
    {
        await using var fixture = await PostgreSqlApiFixture.StartAsync(
            connectionString,
            migrationConnectionString);
        await EnsureTenantAsync(
            administrationConnectionString,
            Guid.Parse("a46f716d-6f13-4e98-a7e0-a04228ba0a90"),
            "stage6r4-test-other");
        const string sharedKey = "same-key-across-tenants";

        using var tenantAEditor = fixture.AuthenticatedClient("idem-a-owner", "Editor", "org-mvs01");
        using var tenantACreate = await tenantAEditor.PostAsJsonAsync("/api/admin/questions", ValidContent("idem a"));
        var tenantAQuestion = await ReadQuestionAsync(tenantACreate);
        using var tenantASubmit = await tenantAEditor.PostAsync($"/api/admin/questions/{tenantAQuestion.Id}/submit", null);
        SpecAssert.Equal(HttpStatusCode.OK, tenantASubmit.StatusCode, "Tenant A precondition must submit.");
        using var tenantAReviewer = fixture.AuthenticatedClient("idem-a-reviewer", "Reviewer", "org-mvs01");
        using var tenantAApprove = await ApproveAsync(
            tenantAReviewer,
            tenantAQuestion.Id,
            sharedKey,
            expectedVersion: 2);
        SpecAssert.Equal(HttpStatusCode.OK, tenantAApprove.StatusCode, "Tenant A approval must succeed.");

        using var tenantBEditor = fixture.AuthenticatedClient("idem-b-owner", "Editor", "org-other");
        using var tenantBCreate = await tenantBEditor.PostAsJsonAsync("/api/admin/questions", ValidContent("idem b"));
        var tenantBQuestion = await ReadQuestionAsync(tenantBCreate);
        using var tenantBSubmit = await tenantBEditor.PostAsync($"/api/admin/questions/{tenantBQuestion.Id}/submit", null);
        SpecAssert.Equal(HttpStatusCode.OK, tenantBSubmit.StatusCode, "Tenant B precondition must submit.");
        using var tenantBReviewer = fixture.AuthenticatedClient("idem-b-reviewer", "Reviewer", "org-other");
        using var tenantBApprove = await ApproveAsync(
            tenantBReviewer,
            tenantBQuestion.Id,
            sharedKey,
            expectedVersion: 2);
        SpecAssert.Equal(
            HttpStatusCode.OK,
            tenantBApprove.StatusCode,
            "The same external key in another tenant must not collide.");

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var migrationDataSource = NpgsqlDataSource.Create(migrationConnectionString);
        var tenantB = Guid.Parse("a46f716d-6f13-4e98-a7e0-a04228ba0a90");
        var countA = await CountIdempotencyAsync(dataSource, TenantIds.Mvs01, sharedKey);
        var countB = await CountIdempotencyAsync(dataSource, tenantB, sharedKey);
        SpecAssert.Equal(1L, countA, "Tenant A must see exactly its own idempotency record.");
        SpecAssert.Equal(1L, countB, "Tenant B must see exactly its own idempotency record.");

        await ExpireIdempotencyAsync(migrationDataSource, TenantIds.Mvs01, sharedKey);
        using var tenantASecondCreate = await tenantAEditor.PostAsJsonAsync(
            "/api/admin/questions",
            ValidContent("idem a after expiry"));
        var tenantASecondQuestion = await ReadQuestionAsync(tenantASecondCreate);
        using var tenantASecondSubmit = await tenantAEditor.PostAsync(
            $"/api/admin/questions/{tenantASecondQuestion.Id}/submit",
            null);
        SpecAssert.Equal(HttpStatusCode.OK, tenantASecondSubmit.StatusCode, "Expired-key reuse precondition must submit.");
        using var tenantAReusedAfterExpiry = await ApproveAsync(
            tenantAReviewer,
            tenantASecondQuestion.Id,
            sharedKey,
            expectedVersion: 2);
        SpecAssert.Equal(
            HttpStatusCode.OK,
            tenantAReusedAfterExpiry.StatusCode,
            "An expired record must be cleaned so the key can protect a new command after 24 hours.");
        var countAfterReuse = await CountIdempotencyAsync(dataSource, TenantIds.Mvs01, sharedKey);
        SpecAssert.Equal(1L, countAfterReuse, "Cleanup and reuse must leave one current Tenant A record.");
    }),
    new("TC-ACC-MVS01-075-PG", "MIGRATION-002-003", "Expand/Contract移行を実DBで確定", async () =>
    {
        var schemaName = $"stage6r4_migration_{Guid.NewGuid():N}";
        var legacyQuestionId = Guid.NewGuid();
        await using var administration = NpgsqlDataSource.Create(migrationConnectionString);
        await using (var createSchema = administration.CreateCommand($"CREATE SCHEMA \"{schemaName}\";"))
        {
            await createSchema.ExecuteNonQueryAsync();
        }

        var scopedApplicationConnectionString = new NpgsqlConnectionStringBuilder(connectionString)
        {
            SearchPath = schemaName,
            Pooling = false
        }.ConnectionString;
        var scopedMigrationConnectionString = new NpgsqlConnectionStringBuilder(
            migrationConnectionString)
        {
            SearchPath = schemaName,
            Pooling = false
        }.ConnectionString;
        try
        {
            var initialResourceName = typeof(AppHost).Assembly.GetManifestResourceNames()
                .Single(name => name.EndsWith(".001_initial.sql", StringComparison.Ordinal));
            await using var initialStream = typeof(AppHost).Assembly.GetManifestResourceStream(initialResourceName)
                ?? throw new TestFailureException("The embedded 001 migration was not found.");
            using var initialReader = new StreamReader(initialStream);
            var initialSql = await initialReader.ReadToEndAsync();

            await using (var scopedDataSource = NpgsqlDataSource.Create(scopedMigrationConnectionString))
            await using (var seedConnection = await scopedDataSource.OpenConnectionAsync())
            {
                await using (var seedSchema = new NpgsqlCommand(
                    initialSql
                    + """

                    CREATE TABLE schema_migrations (
                        version varchar(300) PRIMARY KEY,
                        applied_at timestamptz NOT NULL
                    );
                    """,
                    seedConnection))
                {
                    await seedSchema.ExecuteNonQueryAsync();
                }

                await using var seedData = new NpgsqlCommand(
                    """
                    INSERT INTO schema_migrations (version, applied_at)
                    VALUES (@version, now());

                    INSERT INTO questions (
                        id, title, body, tags, status, version, owner_subject,
                        created_at, updated_at, published_at, review_reason)
                    VALUES (
                        @question_id, 'legacy published', 'legacy body', ARRAY['legacy'],
                        'PUBLISHED', 3, 'legacy-owner', now(), now(), now(), NULL);

                    INSERT INTO audit_events (
                        id, actor_subject, target_id, action, result, correlation_id, occurred_at)
                    VALUES (
                        @audit_id, 'legacy-reviewer', @question_id, 'question.approve',
                        'success', 'legacy-correlation', now());

                    INSERT INTO idempotency_records (
                        idempotency_key, fingerprint, response_snapshot, created_at)
                    VALUES (
                        'legacy-key', 'legacy-fingerprint',
                        '{"approvedBy":"legacy-reviewer","approvedVersion":2}'::jsonb,
                        now());
                    """,
                    seedConnection);
                seedData.Parameters.AddWithValue("version", initialResourceName);
                seedData.Parameters.AddWithValue("question_id", legacyQuestionId);
                seedData.Parameters.AddWithValue("audit_id", Guid.NewGuid());
                await seedData.ExecuteNonQueryAsync();
            }

            await using (var first = await PostgreSqlApiFixture.StartAsync(
                scopedApplicationConnectionString,
                scopedMigrationConnectionString))
            {
                using var readyClient = first.AnonymousClient();
                using var ready = await readyClient.GetAsync("/health/ready");
                SpecAssert.Equal(HttpStatusCode.OK, ready.StatusCode, "The migrated schema must become ready.");
            }
            await using (var second = await PostgreSqlApiFixture.StartAsync(
                scopedApplicationConnectionString,
                scopedMigrationConnectionString))
            {
                using var readyClient = second.AnonymousClient();
                using var ready = await readyClient.GetAsync("/health/ready");
                SpecAssert.Equal(HttpStatusCode.OK, ready.StatusCode, "Reapplying migrations must be idempotent.");
            }

            await using var verificationDataSource = NpgsqlDataSource.Create(
                scopedMigrationConnectionString);
            await using var connection = await verificationDataSource.OpenConnectionAsync();
            await using (var migrations = new NpgsqlCommand(
                "SELECT count(*) FROM schema_migrations;",
                connection))
            {
                var count = (long)(await migrations.ExecuteScalarAsync() ?? 0L);
                SpecAssert.Equal(4L, count, "001 through 004 migrations must each be recorded once.");
            }

            await using (var defaults = new NpgsqlCommand(
                """
                SELECT count(*)
                FROM information_schema.columns
                WHERE table_schema = @schema_name
                  AND column_name = 'tenant_id'
                  AND table_name = ANY (ARRAY['questions', 'audit_events', 'idempotency_records'])
                  AND column_default IS NULL
                  AND is_nullable = 'NO';
                """,
                connection))
            {
                defaults.Parameters.AddWithValue("schema_name", schemaName);
                var contracted = (long)(await defaults.ExecuteScalarAsync() ?? 0L);
                SpecAssert.Equal(3L, contracted, "Contract migration must remove every temporary tenant default.");
            }

            await using var transaction = await connection.BeginTransactionAsync();
            await SetTenantAsync(connection, transaction, TenantIds.Mvs01);
            await using var migrated = new NpgsqlCommand(
                """
                SELECT count(*)
                FROM questions AS question
                JOIN question_revisions AS revision
                  ON revision.tenant_id = question.tenant_id
                 AND revision.question_id = question.id
                 AND revision.id = question.published_revision_id
                WHERE question.id = @question_id
                  AND question.tenant_id = @tenant_id
                  AND question.approved_by = 'migrated:unknown'
                  AND question.approved_version = revision.version
                  AND question.withdrawal_reason IS NULL
                  AND EXISTS (
                      SELECT 1 FROM audit_events
                      WHERE tenant_id = @tenant_id AND target_id = @question_id)
                  AND EXISTS (
                      SELECT 1 FROM idempotency_records
                      WHERE tenant_id = @tenant_id
                        AND idempotency_key = 'legacy-key'
                        AND actor_subject = 'legacy-reviewer'
                        AND expected_version = 2
                        AND expires_at > created_at);
                """,
                connection,
                transaction);
            migrated.Parameters.AddWithValue("question_id", legacyQuestionId);
            migrated.Parameters.AddWithValue("tenant_id", TenantIds.Mvs01);
            var migratedCount = (long)(await migrated.ExecuteScalarAsync() ?? 0L);
            SpecAssert.Equal(1L, migratedCount, "Existing 001 data must be expanded and contracted without losing attribution.");
            await transaction.CommitAsync();
        }
        finally
        {
            await using var dropSchema = administration.CreateCommand($"DROP SCHEMA \"{schemaName}\" CASCADE;");
            await dropSchema.ExecuteNonQueryAsync();
        }
    }),
    new("TC-ACC-MVS01-024", "REQ-MVS01-DAT-001", "起動時に管理対象スキーマを一度だけ適用", async () =>
    {
        await using (var fixture = await PostgreSqlApiFixture.StartAsync(
            connectionString,
            migrationConnectionString))
        {
            using var client = fixture.AnonymousClient();
            var ready = await client.GetAsync("/health/ready");
            SpecAssert.Equal(HttpStatusCode.OK, ready.StatusCode, "Migrated store must report ready.");
        }

        await using var dataSource = NpgsqlDataSource.Create(migrationConnectionString);
        await using var command = dataSource.CreateCommand(
            "SELECT count(*) FROM schema_migrations WHERE version LIKE '%.001_initial.sql';");
        var applied = (long)(await command.ExecuteScalarAsync() ?? 0L);
        SpecAssert.Equal(1L, applied, "The initial migration must be recorded exactly once.");
    }),
    new("TC-ACC-MVS01-025", "REQ-MVS01-DAT-002", "承認・監査・冪等応答を同一永続層で一回だけ確定", async () =>
    {
        await using var fixture = await PostgreSqlApiFixture.StartAsync(
            connectionString,
            migrationConnectionString);
        var published = await PublishAsync(fixture, "transactional publication", "tx-owner", "tx-reviewer");
        using var reviewer = fixture.AuthenticatedClient("tx-reviewer", "Reviewer");
        var retry = await ApproveAsync(
            reviewer,
            published.Id,
            $"approve-{published.Id}",
            expectedVersion: published.Version - 1);
        var retryResult = await ReadQuestionAsync(retry);
        SpecAssert.Equal(published.Version, retryResult.Version, "An approval retry must return the stored response.");

        using var auditor = fixture.AuthenticatedClient("tx-auditor", "Auditor");
        var auditResponse = await auditor.GetAsync($"/api/ops/audit/questions/{published.Id}?limit=50");
        var audit = await auditResponse.Content.ReadFromJsonAsync<JsonElement>();
        var approvalCount = audit.EnumerateArray().Count(record =>
            record.GetProperty("targetId").GetGuid() == published.Id
            && record.GetProperty("action").GetString() == "question.approve"
            && record.GetProperty("result").GetString() == "success");
        SpecAssert.Equal(1, approvalCount, "A committed approval must have exactly one success audit event.");
    }),
    new("TC-ACC-MVS01-026", "REQ-MVS01-AVL-001", "アプリ再起動後も公開データを取得可能", async () =>
    {
        QuestionResponse published;
        await using (var first = await PostgreSqlApiFixture.StartAsync(
            connectionString,
            migrationConnectionString))
        {
            published = await PublishAsync(first, "restart persistence", "restart-owner", "restart-reviewer");
        }

        await using var second = await PostgreSqlApiFixture.StartAsync(
            connectionString,
            migrationConnectionString);
        using var publicClient = second.AnonymousClient();
        var response = await publicClient.GetAsync($"/api/public/questions/{published.Id}");
        SpecAssert.Equal(HttpStatusCode.OK, response.StatusCode, "Published data must survive application restart.");
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        SpecAssert.Equal(published.Id, payload.GetProperty("id").GetGuid(), "Restarted API must return the same question.");
    }),
    new("TC-ACC-MVS01-058", "REQ-MVS01-SEC-006", "PostgreSQL管理一覧を所有者とReviewer scopeで分離", async () =>
    {
        await using var fixture = await PostgreSqlApiFixture.StartAsync(
            connectionString,
            migrationConnectionString);
        using var owner = fixture.AuthenticatedClient("pg-list-owner", "Editor");
        using var other = fixture.AuthenticatedClient("pg-list-other", "Editor");
        using var mineResponse = await owner.PostAsJsonAsync("/api/admin/questions", ValidContent("pg owner"));
        var mine = await ReadQuestionAsync(mineResponse);
        using var otherResponse = await other.PostAsJsonAsync("/api/admin/questions", ValidContent("pg other"));
        _ = await ReadQuestionAsync(otherResponse);

        using var ownerListResponse = await owner.GetAsync("/api/admin/questions?limit=100");
        var ownerList = await ReadQuestionsAsync(ownerListResponse);
        SpecAssert.True(ownerList.Any(question => question.Id == mine.Id), "The PostgreSQL owner list must contain the owner's draft.");
        SpecAssert.True(
            ownerList.All(question => question.OwnerSubject == "pg-list-owner"),
            "The PostgreSQL owner query must not disclose another Editor's row.");

        using var submitted = await owner.PostAsync($"/api/admin/questions/{mine.Id}/submit", null);
        SpecAssert.Equal(HttpStatusCode.OK, submitted.StatusCode, "The PostgreSQL review-queue precondition must submit.");
        using var reviewer = fixture.AuthenticatedClient("pg-list-reviewer", "Reviewer");
        using var queueResponse = await reviewer.GetAsync("/api/admin/questions?status=IN_REVIEW&limit=100");
        var queue = await ReadQuestionsAsync(queueResponse);
        SpecAssert.True(queue.Any(question => question.Id == mine.Id), "The PostgreSQL Reviewer query must contain the submitted row.");
        SpecAssert.True(queue.All(question => question.Status == QuestionStatus.InReview), "The PostgreSQL status filter must be enforced.");
    }),
    new("TC-ACC-MVS01-027", "REQ-MVS01-AVL-002", "DB停止時は接続情報を漏らさず503", async () =>
    {
        await using var fixture = await PostgreSqlApiFixture.StartAsync(
            connectionString,
            migrationConnectionString);
        await StopPostgreSqlAsync();

        using var client = fixture.AnonymousClient();
        var ready = await client.GetAsync("/health/ready");
        SpecAssert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode, "Readiness must fail closed when PostgreSQL stops.");

        var response = await client.GetAsync($"/api/public/questions/{Guid.NewGuid()}");
        var wire = await response.Content.ReadAsStringAsync();
        SpecAssert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode, "Data API must return 503 when PostgreSQL stops.");
        SpecAssert.False(wire.Contains("Npgsql", StringComparison.OrdinalIgnoreCase), "Provider internals must not be disclosed.");
        SpecAssert.False(wire.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase), "Database host must not be disclosed.");
        SpecAssert.False(wire.Contains("Host=", StringComparison.OrdinalIgnoreCase), "Connection string must not be disclosed.");
    })
};

return await SpecTestRunner.RunAsync("ToiNoMori PostgreSQL integration tests", tests);

static QuestionContentRequest ValidContent(string suffix) =>
    new($"question {suffix}", $"body {suffix}", ["cloud", "library"]);

static async Task<QuestionResponse> PublishAsync(
    PostgreSqlApiFixture fixture,
    string title,
    string ownerSubject,
    string reviewerSubject)
{
    using var editor = fixture.AuthenticatedClient(ownerSubject, "Editor");
    var createdResponse = await editor.PostAsJsonAsync("/api/admin/questions", ValidContent(title));
    SpecAssert.Equal(HttpStatusCode.Created, createdResponse.StatusCode, "Test precondition create must succeed.");
    var created = await ReadQuestionAsync(createdResponse);

    var submitted = await editor.PostAsync($"/api/admin/questions/{created.Id}/submit", null);
    SpecAssert.Equal(HttpStatusCode.OK, submitted.StatusCode, "Test precondition submit must succeed.");

    using var reviewer = fixture.AuthenticatedClient(reviewerSubject, "Reviewer");
    var approved = await ApproveAsync(
        reviewer,
        created.Id,
        $"approve-{created.Id}",
        expectedVersion: created.Version + 1);
    SpecAssert.Equal(HttpStatusCode.OK, approved.StatusCode, "Test precondition approve must succeed.");
    return await ReadQuestionAsync(approved);
}

static Task<HttpResponseMessage> ApproveAsync(
    HttpClient client,
    Guid id,
    string idempotencyKey,
    int expectedVersion)
{
    var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/questions/{id}/approve")
    {
        Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
    };
    request.Headers.Add("Idempotency-Key", idempotencyKey);
    request.Headers.TryAddWithoutValidation("If-Match", $"\"{expectedVersion}\"");
    return client.SendAsync(request);
}

static async Task<QuestionResponse> ReadQuestionAsync(HttpResponseMessage response)
{
    var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper));
    return await response.Content.ReadFromJsonAsync<QuestionResponse>(options)
        ?? throw new TestFailureException("Question response JSON was empty.");
}

static async Task<QuestionResponse[]> ReadQuestionsAsync(HttpResponseMessage response)
{
    var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper));
    return await response.Content.ReadFromJsonAsync<QuestionResponse[]>(options)
        ?? throw new TestFailureException("Question list response JSON was empty.");
}

static async Task SetTenantAsync(
    NpgsqlConnection connection,
    NpgsqlTransaction transaction,
    Guid tenantId)
{
    await using var command = new NpgsqlCommand(
        "SELECT set_config('app.tenant_id', @tenant_id, true);",
        connection,
        transaction);
    command.Parameters.AddWithValue("tenant_id", tenantId.ToString("D"));
    await command.ExecuteScalarAsync();
}

static async Task EnsureTenantAsync(
    string databaseConnectionString,
    Guid tenantId,
    string tenantCode)
{
    await using var dataSource = NpgsqlDataSource.Create(databaseConnectionString);
    await using var command = dataSource.CreateCommand(
        """
        INSERT INTO tenants (id, tenant_code, display_name, is_active, created_at)
        VALUES (@id, @tenant_code, @display_name, true, now())
        ON CONFLICT (id) DO NOTHING;
        """);
    command.Parameters.AddWithValue("id", tenantId);
    command.Parameters.AddWithValue("tenant_code", tenantCode);
    command.Parameters.AddWithValue("display_name", tenantCode);
    await command.ExecuteNonQueryAsync();
}

static async Task AssertRoleBoundaryRejectedAsync(
    string candidateConnectionString,
    string message)
{
    await using var candidate = new PostgreSqlApplicationDataSource(
        NpgsqlDataSource.Create(candidateConnectionString));
    var validator = new PostgreSqlRoleBoundaryValidator(candidate);
    try
    {
        await validator.ValidateAsync(CancellationToken.None);
    }
    catch (InvalidOperationException exception)
        when (exception.Message.Contains("least-privilege boundary", StringComparison.Ordinal))
    {
        return;
    }

    throw new TestFailureException(message);
}

static async Task<long> CountQuestionAsync(
    NpgsqlDataSource dataSource,
    Guid tenantId,
    Guid questionId)
{
    await using var connection = await dataSource.OpenConnectionAsync();
    await using var transaction = await connection.BeginTransactionAsync();
    await SetTenantAsync(connection, transaction, tenantId);
    await using var command = new NpgsqlCommand(
        "SELECT count(*) FROM questions WHERE id = @id;",
        connection,
        transaction);
    command.Parameters.AddWithValue("id", questionId);
    var count = (long)(await command.ExecuteScalarAsync() ?? 0L);
    await transaction.CommitAsync();
    return count;
}

static async Task<long> CountQuestionWithRawSettingAsync(
    NpgsqlDataSource dataSource,
    string tenantSetting,
    Guid questionId)
{
    await using var connection = await dataSource.OpenConnectionAsync();
    await using var transaction = await connection.BeginTransactionAsync();
    await using (var setting = new NpgsqlCommand(
        "SELECT set_config('app.tenant_id', @tenant_id, true);",
        connection,
        transaction))
    {
        setting.Parameters.AddWithValue("tenant_id", tenantSetting);
        await setting.ExecuteScalarAsync();
    }

    await using var command = new NpgsqlCommand(
        "SELECT count(*) FROM questions WHERE id = @id;",
        connection,
        transaction);
    command.Parameters.AddWithValue("id", questionId);
    var count = (long)(await command.ExecuteScalarAsync() ?? 0L);
    await transaction.CommitAsync();
    return count;
}

static async Task<long> CountQuestionWithoutSettingAsync(
    NpgsqlDataSource dataSource,
    Guid questionId)
{
    await using var connection = await dataSource.OpenConnectionAsync();
    await using var transaction = await connection.BeginTransactionAsync();
    await using var command = new NpgsqlCommand(
        "SELECT count(*) FROM questions WHERE id = @id;",
        connection,
        transaction);
    command.Parameters.AddWithValue("id", questionId);
    var count = (long)(await command.ExecuteScalarAsync() ?? 0L);
    await transaction.CommitAsync();
    return count;
}

static async Task<long> CountIdempotencyAsync(
    NpgsqlDataSource dataSource,
    Guid tenantId,
    string idempotencyKey)
{
    await using var connection = await dataSource.OpenConnectionAsync();
    await using var transaction = await connection.BeginTransactionAsync();
    await SetTenantAsync(connection, transaction, tenantId);
    await using var command = new NpgsqlCommand(
        """
        SELECT count(*)
        FROM idempotency_records
        WHERE idempotency_key = @key
          AND actor_subject <> ''
          AND expected_version >= 1
          AND expires_at > created_at;
        """,
        connection,
        transaction);
    command.Parameters.AddWithValue("key", idempotencyKey);
    var count = (long)(await command.ExecuteScalarAsync() ?? 0L);
    await transaction.CommitAsync();
    return count;
}

static async Task ExpireIdempotencyAsync(
    NpgsqlDataSource dataSource,
    Guid tenantId,
    string idempotencyKey)
{
    await using var connection = await dataSource.OpenConnectionAsync();
    await using var transaction = await connection.BeginTransactionAsync();
    await SetTenantAsync(connection, transaction, tenantId);
    await using var command = new NpgsqlCommand(
        """
        UPDATE idempotency_records
        SET expires_at = now() - interval '1 second'
        WHERE tenant_id = @tenant_id AND idempotency_key = @key;
        """,
        connection,
        transaction);
    command.Parameters.AddWithValue("tenant_id", tenantId);
    command.Parameters.AddWithValue("key", idempotencyKey);
    var updated = await command.ExecuteNonQueryAsync();
    SpecAssert.Equal(1, updated, "The test must expire exactly one scoped idempotency record.");
    await transaction.CommitAsync();
}

static async Task StopPostgreSqlAsync()
{
    var pgCtl = Environment.GetEnvironmentVariable("MVS01_TEST_PG_CTL")
        ?? throw new InvalidOperationException("MVS01_TEST_PG_CTL is required.");
    var dataDirectory = Environment.GetEnvironmentVariable("MVS01_TEST_PG_DATA")
        ?? throw new InvalidOperationException("MVS01_TEST_PG_DATA is required.");
    var runAs = Environment.GetEnvironmentVariable("MVS01_TEST_PG_RUN_AS");

    var startInfo = new ProcessStartInfo
    {
        FileName = string.IsNullOrWhiteSpace(runAs) ? pgCtl : "/usr/sbin/runuser",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
    };
    if (!string.IsNullOrWhiteSpace(runAs))
    {
        startInfo.ArgumentList.Add("-u");
        startInfo.ArgumentList.Add(runAs);
        startInfo.ArgumentList.Add("--");
        startInfo.ArgumentList.Add(pgCtl);
    }

    startInfo.ArgumentList.Add("-D");
    startInfo.ArgumentList.Add(dataDirectory);
    startInfo.ArgumentList.Add("-m");
    startInfo.ArgumentList.Add("immediate");
    startInfo.ArgumentList.Add("stop");

    using var process = Process.Start(startInfo)
        ?? throw new InvalidOperationException("Could not start pg_ctl.");
    var standardOutput = process.StandardOutput.ReadToEndAsync();
    var standardError = process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();
    if (process.ExitCode != 0)
    {
        throw new TestFailureException(
            $"pg_ctl stop failed ({process.ExitCode}): {await standardOutput} {await standardError}");
    }
}
