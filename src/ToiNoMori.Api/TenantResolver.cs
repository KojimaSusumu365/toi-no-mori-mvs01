using System.Security.Claims;

namespace ToiNoMori.Api;

public sealed class TenantResolver
{
    public const string ExternalOrganizationClaimType = "external_organization_id";
    public const string VerifiedIssuerClaimType = "verified_issuer";
    public const string InternalTenantClaimType = "internal_tenant_id";
    public const string HttpContextItemName = "internal_tenant_id";

    private readonly Dictionary<(string Issuer, string ExternalOrganizationId), Guid> _organizations;

    public TenantResolver(IConfiguration configuration)
    {
        var organizations = new Dictionary<(string Issuer, string ExternalOrganizationId), Guid>();
        foreach (var entry in configuration.GetSection("Tenancy:Organizations").GetChildren())
        {
            var issuer = entry["Issuer"];
            var externalOrganizationId = entry["ExternalOrganizationId"];
            if (string.IsNullOrWhiteSpace(issuer)
                || !Uri.TryCreate(issuer, UriKind.Absolute, out _)
                || string.IsNullOrWhiteSpace(externalOrganizationId)
                || !Guid.TryParse(entry["InternalTenantId"], out var tenantId)
                || tenantId == Guid.Empty
                || !organizations.TryAdd((issuer, externalOrganizationId), tenantId))
            {
                throw new InvalidOperationException(
                    "Every Tenancy:Organizations entry must map one absolute issuer and external organization ID to one non-empty internal tenant UUID.");
            }
        }

        _organizations = organizations;
    }

    public Guid Resolve(ClaimsPrincipal principal)
    {
        var internalTenantClaims = principal.FindAll(InternalTenantClaimType).ToArray();
        var organizationClaims = principal.FindAll(ExternalOrganizationClaimType).ToArray();
        var issuerClaims = principal.FindAll(VerifiedIssuerClaimType).ToArray();

        if (internalTenantClaims.Length > 0)
        {
            if (internalTenantClaims.Length != 1
                || organizationClaims.Length != 0
                || issuerClaims.Length != 0
                || !Guid.TryParse(internalTenantClaims[0].Value, out var internalTenantId)
                || internalTenantId == Guid.Empty)
            {
                throw InvalidOrUnmapped();
            }

            return internalTenantId;
        }

        if (issuerClaims.Length != 1)
        {
            throw InvalidOrUnmapped();
        }

        return ResolveExternal(
            issuerClaims[0].Value,
            organizationClaims.Select(claim => claim.Value));
    }

    public Guid ResolveExternal(
        string verifiedIssuer,
        IEnumerable<string> externalOrganizationIds)
    {
        var organizationIds = externalOrganizationIds.ToArray();
        if (organizationIds.Length == 0
            || organizationIds.All(string.IsNullOrWhiteSpace))
        {
            throw new TenantResolutionException(
                "tenant.claim_missing",
                "A verified organization claim is required.");
        }

        if (organizationIds.Length != 1
            || string.IsNullOrWhiteSpace(organizationIds[0])
            || string.IsNullOrWhiteSpace(verifiedIssuer)
            || !_organizations.TryGetValue(
                (verifiedIssuer, organizationIds[0]),
                out var tenantId))
        {
            throw InvalidOrUnmapped();
        }

        return tenantId;
    }

    private static TenantResolutionException InvalidOrUnmapped() => new(
        "tenant.claim_invalid_or_unmapped",
        "The verified organization claim is not registered.");

    public static Guid Current(HttpContext context) =>
        context.Items.TryGetValue(HttpContextItemName, out var value) && value is Guid tenantId
            ? tenantId
            : throw new InvalidOperationException("The internal tenant context was not established.");
}

public sealed class TenantResolutionException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
