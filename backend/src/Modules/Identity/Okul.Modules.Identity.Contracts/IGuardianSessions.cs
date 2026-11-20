namespace Okul.Modules.Identity.Contracts;

public enum SessionClient
{
    Web = 1,
    Mobile = 2,
}

/// <summary>
/// A veli session just opened (spec 0007, Requirement 7): what the API needs to issue the access and refresh credentials.
/// Roles hold the veli role only.
/// </summary>
public sealed record GuardianSessionStarted(
    Guid SessionId, Guid TenantId, Guid GuardianId, IReadOnlyList<string> Roles, SessionClient Client,
    DateTimeOffset ExpiresAt, string RefreshToken);

/// <summary>
/// Veli sessions (ADR 0002): the Kayıt module verifies the veli, Kimlik opens and closes the session — the same rules as a
/// staff session (spec 0003: 15-minute access, silent renewal, web 12 hours / mobile 30 days, logout closes at once).
/// </summary>
public interface IGuardianSessions
{
    Task<GuardianSessionStarted> StartAsync(
        Guid tenantId, Guid guardianId, SessionClient client, CancellationToken cancellationToken = default);

    /// <summary>Closes every open session of the veli (the reset command, Requirement 10); the number closed.</summary>
    Task<int> CloseAllAsync(Guid tenantId, Guid guardianId, CancellationToken cancellationToken = default);
}
