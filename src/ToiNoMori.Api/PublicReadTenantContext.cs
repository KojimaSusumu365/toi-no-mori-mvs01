namespace ToiNoMori.Api;

/// <summary>
/// Resolves the only tenant exposed through the anonymous Public Read boundary.
/// A second effective tenant is an architecture change, not a data query.
/// </summary>
public sealed class PublicReadTenantContext
{
    public const string SingleTenantMode = "single_tenant";

    public PublicReadTenantContext(IConfiguration configuration)
    {
        var mode = configuration["PublicRead:Mode"];
        var configuredTenantIds = configuration
            .GetSection("PublicRead:TenantIds")
            .GetChildren()
            .Select(entry => entry.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        if (!string.Equals(mode, SingleTenantMode, StringComparison.OrdinalIgnoreCase)
            || configuredTenantIds.Length != 1
            || !Guid.TryParse(configuredTenantIds[0], out var tenantId)
            || tenantId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Public Read Architecture Gate: PublicRead:Mode must be single_tenant and "
                + "PublicRead:TenantIds must contain exactly one non-empty UUID. "
                + "A second public tenant requires an approved tenant-context design. "
                + "Owner: System Architect.");
        }

        TenantId = tenantId;
    }

    public Guid TenantId { get; }
}
