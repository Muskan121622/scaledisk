namespace ScaleDesk.Application.Constants;

/// <summary>
/// Custom JWT claim types shared by the token issuer (Infrastructure) and the request
/// principal reader (Api), so the two never drift apart on string literals.
/// </summary>
public static class AppClaims
{
    public const string TenantId = "tid";
    public const string Subject = "sub";
    public const string Role = "role";
    public const string Permission = "perm";
    public const string Email = "email";

    /// <summary>Session kind: staff (workspace member), platform (ScaleDesk HQ) or portal (customer).</summary>
    public const string Scope = "scope";
    public const string ScopeStaff = "staff";
    public const string ScopePlatform = "platform";
    public const string ScopePortal = "portal";

    /// <summary>Customer record id for portal-scoped tokens (drives ownership checks without a tenant claim).</summary>
    public const string CustomerId = "cid";
}
