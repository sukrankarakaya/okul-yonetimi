namespace Okul.Modules.Identity.Contracts;

/// <summary>
/// Is this session still open? Asked on EVERY authenticated request (spec 0003, Requirements 6–7; G5): logout and
/// password change take effect on the next request, not when the 15-minute access expires.
/// </summary>
public interface ISessionValidator
{
    Task<bool> IsActiveAsync(Guid tenantId, Guid sessionId, CancellationToken cancellationToken = default);
}
