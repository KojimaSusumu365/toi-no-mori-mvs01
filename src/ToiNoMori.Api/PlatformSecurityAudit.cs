using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using Microsoft.AspNetCore.Routing;

namespace ToiNoMori.Api;

public sealed record AccessDenialAuditEnvelope(
    Guid Id,
    DateTimeOffset OccurredAt,
    string ReasonCode,
    string NormalizedAction,
    string PartitionHash,
    string RequestId,
    string CorrelationId,
    DateTimeOffset? WindowStartedAt);

public sealed record AuditOutcomeRecorded(bool Recorded, bool Suppressed)
{
    public static AuditOutcomeRecorded Written { get; } = new(true, false);

    public static AuditOutcomeRecorded DuplicateSuppressed { get; } = new(false, true);
}

public interface IAuditSink
{
    Task<AuditOutcomeRecorded> WriteAsync(
        AccessDenialAuditEnvelope envelope,
        CancellationToken cancellationToken);
}

public sealed record PlatformSecurityEvent(
    Guid Id,
    DateTimeOffset OccurredAt,
    string ReasonCode,
    string NormalizedAction,
    string PartitionHash,
    string RequestId,
    string CorrelationId,
    int OccurrenceCount,
    DateTimeOffset? WindowStartedAt);

public sealed record PlatformSecurityEventResponse(
    DateTimeOffset OccurredAt,
    string ReasonCode,
    string NormalizedAction,
    string RequestId,
    string CorrelationId,
    int OccurrenceCount)
{
    public static PlatformSecurityEventResponse From(PlatformSecurityEvent value) => new(
        value.OccurredAt,
        value.ReasonCode,
        value.NormalizedAction,
        value.RequestId,
        value.CorrelationId,
        value.OccurrenceCount);
}

public interface IPlatformSecurityEventReader
{
    Task<IReadOnlyList<PlatformSecurityEvent>> ReadAsync(
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive,
        int limit,
        CancellationToken cancellationToken);
}

public sealed record SecurityAuditMetricsSnapshot(
    long SecurityAuditSuppressedTotal,
    long AuditWriteFailuresTotal,
    long AuditWriteDurationMilliseconds);

public sealed class SecurityAuditMetrics
{
    private long securityAuditSuppressedTotal;
    private long auditWriteFailuresTotal;
    private long auditWriteDurationMilliseconds;

    public SecurityAuditMetricsSnapshot Snapshot() => new(
        Interlocked.Read(ref securityAuditSuppressedTotal),
        Interlocked.Read(ref auditWriteFailuresTotal),
        Interlocked.Read(ref auditWriteDurationMilliseconds));

    internal void RecordSuppressed() => Interlocked.Increment(ref securityAuditSuppressedTotal);

    internal void RecordWriteFailure() => Interlocked.Increment(ref auditWriteFailuresTotal);

    internal void RecordWriteDuration(TimeSpan duration) =>
        Interlocked.Add(ref auditWriteDurationMilliseconds, Math.Max(0L, (long)duration.TotalMilliseconds));
}

public sealed class InMemoryPlatformSecurityAuditStore
    : IAuditSink, IPlatformSecurityEventReader
{
    private readonly object gate = new();
    private readonly List<PlatformSecurityEvent> events = [];

    public Task<AuditOutcomeRecorded> WriteAsync(
        AccessDenialAuditEnvelope envelope,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            if (envelope.ReasonCode == SecurityAuditReasons.RateLimited
                && events.Any(item =>
                    item.ReasonCode == envelope.ReasonCode
                    && item.PartitionHash == envelope.PartitionHash
                    && item.NormalizedAction == envelope.NormalizedAction
                    && item.WindowStartedAt == envelope.WindowStartedAt))
            {
                return Task.FromResult(AuditOutcomeRecorded.DuplicateSuppressed);
            }

            events.Add(new(
                envelope.Id,
                envelope.OccurredAt,
                envelope.ReasonCode,
                envelope.NormalizedAction,
                envelope.PartitionHash,
                envelope.RequestId,
                envelope.CorrelationId,
                1,
                envelope.WindowStartedAt));
        }

        return Task.FromResult(AuditOutcomeRecorded.Written);
    }

    public Task<IReadOnlyList<PlatformSecurityEvent>> ReadAsync(
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive,
        int limit,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            IReadOnlyList<PlatformSecurityEvent> result = events
                .Where(item => item.OccurredAt >= fromInclusive && item.OccurredAt < toExclusive)
                .OrderByDescending(item => item.OccurredAt)
                .ThenByDescending(item => item.Id)
                .Take(limit)
                .ToArray();
            return Task.FromResult(result);
        }
    }
}

internal static class SecurityAuditReasons
{
    internal const string Unauthenticated = "access.unauthenticated";
    internal const string Forbidden = "access.forbidden";
    internal const string TenantClaimMissing = "tenant.claim_missing";
    internal const string TenantClaimInvalidOrUnmapped = "tenant.claim_invalid_or_unmapped";
    internal const string CsrfMissingOrInvalid = "csrf.missing_or_invalid";
    internal const string RateLimited = "access.rate_limited";
    internal const string ResourceNotVisibleOrMissing = "resource.not_visible_or_missing";
}

internal static class SecurityAuditContext
{
    private const string ReasonItem = "security_audit_reason";

    internal static void MarkReason(HttpContext context, string reasonCode) =>
        context.Items[ReasonItem] = reasonCode;

    internal static string? Reason(HttpContext context) =>
        context.Items.TryGetValue(ReasonItem, out var value) ? value as string : null;
}

internal sealed class SecurityAuditPartitionHasher
{
    private readonly byte[] key;

    public SecurityAuditPartitionHasher(IConfiguration configuration, IHostEnvironment environment)
    {
        var encodedKey = configuration["Audit:PartitionHashKey"];
        if (!TryDecodeKey(encodedKey, out key))
        {
            if (environment.IsProduction())
            {
                throw new InvalidOperationException(
                    "Production requires Audit:PartitionHashKey as at least 32 bytes of Base64 key material.");
            }

            key = RandomNumberGenerator.GetBytes(32);
        }
    }

    internal string Hash(string partitionValue)
    {
        using var hmac = new HMACSHA256(key);
        return Convert.ToHexString(
            hmac.ComputeHash(Encoding.UTF8.GetBytes(partitionValue)))
            .ToLowerInvariant();
    }

    private static bool TryDecodeKey(string? encodedKey, out byte[] decoded)
    {
        try
        {
            decoded = string.IsNullOrWhiteSpace(encodedKey)
                ? []
                : Convert.FromBase64String(encodedKey);
            return decoded.Length >= 32;
        }
        catch (FormatException)
        {
            decoded = [];
            return false;
        }
    }
}

internal sealed class SecurityAuditQueue
{
    private readonly Channel<AccessDenialAuditEnvelope> channel;
    private readonly ConcurrentDictionary<string, DateTimeOffset> rateLimitWindows = new();
    private readonly SecurityAuditMetrics metrics;

    public SecurityAuditQueue(IConfiguration configuration, SecurityAuditMetrics metrics)
    {
        this.metrics = metrics;
        var capacity = configuration.GetValue("Audit:QueueCapacity", 1024);
        if (capacity is < 16 or > 65536)
        {
            throw new InvalidOperationException("Audit:QueueCapacity must be between 16 and 65536.");
        }

        channel = Channel.CreateBounded<AccessDenialAuditEnvelope>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    }

    internal bool TryEnqueue(AccessDenialAuditEnvelope envelope)
    {
        if (envelope.ReasonCode == SecurityAuditReasons.RateLimited)
        {
            var suppressionKey = string.Join(
                ':',
                envelope.PartitionHash,
                envelope.NormalizedAction,
                envelope.WindowStartedAt?.UtcTicks ?? 0L);
            if (!rateLimitWindows.TryAdd(suppressionKey, envelope.OccurredAt))
            {
                metrics.RecordSuppressed();
                return false;
            }

            TrimExpiredWindows(envelope.OccurredAt);
        }

        if (channel.Writer.TryWrite(envelope))
        {
            return true;
        }

        metrics.RecordWriteFailure();
        return false;
    }

    internal IAsyncEnumerable<AccessDenialAuditEnvelope> ReadAllAsync(
        CancellationToken cancellationToken) => channel.Reader.ReadAllAsync(cancellationToken);

    private void TrimExpiredWindows(DateTimeOffset now)
    {
        if (rateLimitWindows.Count < 4096)
        {
            return;
        }

        var threshold = now.AddMinutes(-2);
        foreach (var item in rateLimitWindows)
        {
            if (item.Value < threshold)
            {
                rateLimitWindows.TryRemove(item.Key, out _);
            }
        }
    }
}

internal sealed class SecurityAuditWorker(
    SecurityAuditQueue queue,
    IAuditSink sink,
    SecurityAuditMetrics metrics,
    IConfiguration configuration,
    ILogger<SecurityAuditWorker> logger) : BackgroundService
{
    private static readonly Action<ILogger, string, string, string, Exception?> FallbackLog =
        LoggerMessage.Define<string, string, string>(
            LogLevel.Error,
            new EventId(7001, "PlatformSecurityAuditFallback"),
            "Security audit fallback. Reason={ReasonCode}; request_id={RequestId}; correlation_id={CorrelationId}; failure=audit_sink_unavailable.");

    private readonly TimeSpan writeTimeout = TimeSpan.FromMilliseconds(
        ValidateWriteTimeout(configuration.GetValue("Audit:WriteTimeoutMilliseconds", 250)));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var envelope in queue.ReadAllAsync(stoppingToken))
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                timeout.CancelAfter(writeTimeout);
                var outcome = await sink.WriteAsync(envelope, timeout.Token);
                if (outcome.Suppressed)
                {
                    metrics.RecordSuppressed();
                }
            }
            catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
            {
                RecordFailure(envelope);
            }
            catch (Exception)
            {
                RecordFailure(envelope);
            }
            finally
            {
                stopwatch.Stop();
                metrics.RecordWriteDuration(stopwatch.Elapsed);
            }
        }
    }

    private void RecordFailure(AccessDenialAuditEnvelope envelope)
    {
        metrics.RecordWriteFailure();
        FallbackLog(
            logger,
            envelope.ReasonCode,
            envelope.RequestId,
            envelope.CorrelationId,
            null);
    }

    private static int ValidateWriteTimeout(int milliseconds) =>
        milliseconds is >= 10 and <= 5000
            ? milliseconds
            : throw new InvalidOperationException(
                "Audit:WriteTimeoutMilliseconds must be between 10 and 5000.");
}

internal sealed class SecurityAuditMiddleware(
    RequestDelegate next,
    TimeProvider timeProvider,
    SecurityAuditPartitionHasher partitionHasher,
    SecurityAuditQueue queue)
{
    public async Task InvokeAsync(HttpContext context)
    {
        await next(context);
        var reason = ResolveReason(context);
        if (reason is null)
        {
            return;
        }

        var occurredAt = timeProvider.GetUtcNow();
        var envelope = new AccessDenialAuditEnvelope(
            Guid.NewGuid(),
            occurredAt,
            reason,
            NormalizeAction(context),
            partitionHasher.Hash(context.Connection.RemoteIpAddress?.ToString() ?? "server-partition"),
            CorrelationContext.RequestId(context),
            CorrelationContext.CorrelationId(context),
            reason == SecurityAuditReasons.RateLimited
                ? new DateTimeOffset(
                    occurredAt.Year,
                    occurredAt.Month,
                    occurredAt.Day,
                    occurredAt.Hour,
                    occurredAt.Minute,
                    0,
                    TimeSpan.Zero)
                : null);
        queue.TryEnqueue(envelope);
    }

    private static string? ResolveReason(HttpContext context)
    {
        var marked = SecurityAuditContext.Reason(context);
        if (marked is not null)
        {
            return marked;
        }

        if (context.Response.StatusCode == StatusCodes.Status429TooManyRequests)
        {
            return SecurityAuditReasons.RateLimited;
        }

        var protectedPath = context.Request.Path.StartsWithSegments("/api/admin")
            || context.Request.Path.StartsWithSegments("/api/ops")
            || context.Request.Path.StartsWithSegments("/api/platform")
            || context.Request.Path.StartsWithSegments("/bff");
        if (!protectedPath)
        {
            return null;
        }

        return context.Response.StatusCode switch
        {
            StatusCodes.Status401Unauthorized => SecurityAuditReasons.Unauthenticated,
            StatusCodes.Status403Forbidden => SecurityAuditReasons.Forbidden,
            StatusCodes.Status404NotFound when context.Request.Path.StartsWithSegments("/api/admin") =>
                SecurityAuditReasons.ResourceNotVisibleOrMissing,
            _ => null
        };
    }

    private static string NormalizeAction(HttpContext context)
    {
        var routePattern = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;
        if (!string.IsNullOrWhiteSpace(routePattern))
        {
            return $"{context.Request.Method.ToUpperInvariant()} {routePattern}";
        }

        var path = context.Request.Path;
        var boundary = path.StartsWithSegments("/api/public/questions")
            ? "/api/public/questions"
            : path.StartsWithSegments("/api/admin/questions")
                ? "/api/admin/questions"
                : path.StartsWithSegments("/api/ops/audit")
                    ? "/api/ops/audit"
                    : path.StartsWithSegments("/api/platform/security-events")
                        ? "/api/platform/security-events"
                        : "/protected";
        return $"{context.Request.Method.ToUpperInvariant()} {boundary}";
    }
}
