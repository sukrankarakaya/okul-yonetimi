namespace Okul.Core.Tenancy;

/// <summary>
/// The tenant (kurum) of the current operation. Resolved only from the authenticated user —
/// never from client input (BR-1, spec 0001 AC-3). <c>null</c> = no tenant: queries fail closed.
/// </summary>
public interface ITenantContext
{
    Guid? TenantId { get; }
}
