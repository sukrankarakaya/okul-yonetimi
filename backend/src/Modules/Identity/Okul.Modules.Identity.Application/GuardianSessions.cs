using Okul.Core.Tenancy;
using Okul.Modules.Identity.Contracts;
using Okul.Modules.Identity.Domain;

namespace Okul.Modules.Identity.Application;

/// <summary>Opens and closes veli sessions for the Kayıt module (spec 0007, ADR 0002) with the staff session rules.</summary>
public sealed class GuardianSessions : IGuardianSessions
{
    private readonly ITenantScope _tenantScope;
    private readonly ISessionStore _sessions;
    private readonly TimeProvider _time;

    public GuardianSessions(ITenantScope tenantScope, ISessionStore sessions, TimeProvider time)
    {
        _tenantScope = tenantScope;
        _sessions = sessions;
        _time = time;
    }

    public async Task<GuardianSessionStarted> StartAsync(
        Guid tenantId, Guid guardianId, SessionClient client, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(client))
        {
            throw new ArgumentOutOfRangeException(nameof(client));
        }

        var now = _time.GetUtcNow();
        var sessionId = Guid.CreateVersion7();
        var credential = RefreshCredential.New(tenantId, sessionId);
        var session = Session.Start(
            sessionId, tenantId, guardianId, (ClientKind)client, now, credential.Hash, AccountKind.Guardian);
        using (_tenantScope.Begin(tenantId))
        {
            await _sessions.AddAsync(session, cancellationToken);
            await _sessions.SaveChangesAsync(cancellationToken);
        }

        return new GuardianSessionStarted(
            session.Id, tenantId, guardianId, [StaffRoles.Guardian], client, session.ExpiresAt, credential.ToString());
    }

    public async Task<int> CloseAllAsync(Guid tenantId, Guid guardianId, CancellationToken cancellationToken = default)
    {
        using var scope = _tenantScope.Begin(tenantId);
        return await _sessions.CloseAllAsync(guardianId, _time.GetUtcNow(), cancellationToken);
    }
}
