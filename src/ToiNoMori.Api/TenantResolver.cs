using System.Security.Claims;

namespace ToiNoMori.Api;

public sealed class TenantResolver
{
    public const string ExternalOrganizationClaimType = "external_organization_id";
    public const string VerifiedIssuerClaimType = "verified_issuer";
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
        var organizationClaims = principal.FindAll(ExternalOrganizationClaimType).ToArray();
        if (organizationClaims.Length == 0
            || organizationClaims.All(claim => string.IsNullOrWhiteSpace(claim.Value)))
        {
            throw new TenantResolutionException(
                "tenant.claim_missing",
                "A verified organization claim is required.");
        }

        var issuerClaims = principal.FindAll(VerifiedIssuerClaimType).ToArray();
        if (organizationClaims.Length != 1
            || issuerClaims.Length != 1
            || string.IsNullOrWhiteSpace(organizationClaims[0].Value)
            || string.IsNullOrWhiteSpace(issuerClaims[0].Value)
            || !_organizations.TryGetValue(
                (issuerClaims[0].Value, organizationClaims[0].Value),
                out var tenantId))
        {
            throw new TenantResolutionException(
                "tenant.claim_invalid_or_unmapped",
                "The verified organization claim is not registered.");
        }

        return tenantId;
    }

    public static Guid Current(HttpContext context) =>
        context.Items.TryGetValue(HttpContextItemName, out var value) && value is Guid tenantId
            ? tenantId
            : throw new InvalidOperationException("The internal tenant context was not established.");
}

public sealed class TenantResolutionException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
