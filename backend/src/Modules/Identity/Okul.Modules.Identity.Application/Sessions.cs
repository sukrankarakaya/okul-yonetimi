using NArchitecture.Core.Mediator;
using Okul.Core.Tenancy;
using Okul.Modules.Identity.Contracts;
using Okul.Modules.Identity.Domain;

namespace Okul.Modules.Identity.Application;

public enum RefreshStatus
{
    Renewed,

    /// <summary>A parallel refresh within the grace window: nothing changed, the client retries its request (R5).</summary>
    Conflict,

    /// <summary>Unknown, ended, closed or reused credential: the user logs in again.</summary>
    Rejected,
}

public sealed record RefreshResult(RefreshStatus Status, IssuedSession? Session = null);

/// <summary>Silent renewal (spec 0003, Requirement 5): rotates the refresh credential; reuse closes the session.</summary>
public sealed record RefreshSessionCommand(string? RefreshToken) : IRequest<RefreshResult>;

public sealed class RefreshSessionCommandHandler : IRequestHandler<RefreshSessionCommand, RefreshResult>
{
    private readonly ITenantScope _tenantScope;
    private readonly ISessionStore _sessions;
    private readonly IStaffAccountStore _accounts;
    private readonly TimeProvider _time;

    public RefreshSessionCommandHandler(
        ITenantScope tenantScope, ISessionStore sessions, IStaffAccountStore accounts, TimeProvider time)
    {
        _tenantScope = tenantScope;
        _sessions = sessions;
        _accounts = accounts;
        _time = time;
    }

    public async Task<RefreshResult> Handle(RefreshSessionCommand request, CancellationToken cancellationToken)
    {
        if (!RefreshCredential.TryParse(request.RefreshToken, out var presented))
        {
            return new RefreshResult(RefreshStatus.Rejected);
        }

        var now = _time.GetUtcNow();
        using var scope = _tenantScope.Begin(presented.Value.TenantId);

        var session = await _sessions.FindAsync(presented.Value.SessionId, cancellationToken);
        if (session is null)
        {
            return new RefreshResult(RefreshStatus.Rejected);
        }

        var next = RefreshCredential.New(session.TenantId, session.Id);
        var outcome = session.Refresh(presented.Value.Hash, next.Hash, now);

        switch (outcome)
        {
            case RefreshOutcome.ParallelWithinGrace:
                return new RefreshResult(RefreshStatus.Conflict);
            case RefreshOutcome.Ended:
                return new RefreshResult(RefreshStatus.Rejected);
            case RefreshOutcome.Reused:
                // Theft signal: the session closes even if a rotation landed at the same moment (review round 1 #5).
                if (!await _sessions.TrySaveChangesAsync(cancellationToken))
                {
                    await _sessions.CloseAsync(session.Id, now, cancellationToken);
                }

                return new RefreshResult(RefreshStatus.Rejected);
        }

        // Rotated — but only if nobody rotated the same credential in between: the write is conditional on the hash we
        // read (optimistic concurrency, review round 1 #5). The loser answers 409 like a parallel refresh in the grace.
        if (!await _sessions.TrySaveChangesAsync(cancellationToken))
        {
            return new RefreshResult(RefreshStatus.Conflict);
        }

        var account = await _accounts.FindByIdAsync(session.AccountId, cancellationToken);
        if (account is null || !account.CanLogInAsStaff)
        {
            await _sessions.CloseAsync(session.Id, now, cancellationToken);
            return new RefreshResult(RefreshStatus.Rejected);
        }

        return new RefreshResult(RefreshStatus.Renewed, new IssuedSession(
            session.Id, session.TenantId, account.Id, account.Roles, session.Client, session.ExpiresAt, next.ToString()));
    }
}

/// <summary>
/// Logout (Requirement 6): closes the CURRENT session on the server — the tenant and session come from the validated
/// access claims (an <c>ITenantContext</c> set by authentication), never from client input.
/// </summary>
public sealed record LogoutCommand(Guid SessionId) : IRequest<bool>;

public sealed class LogoutCommandHandler : IRequestHandler<LogoutCommand, bool>
{
    private readonly ISessionStore _sessions;
    private readonly TimeProvider _time;

    public LogoutCommandHandler(ISessionStore sessions, TimeProvider time)
    {
        _sessions = sessions;
        _time = time;
    }

    public async Task<bool> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        // One UPDATE: never collides with a refresh rotating the same session (review round 1 #5).
        return await _sessions.CloseAsync(request.SessionId, _time.GetUtcNow(), cancellationToken);
    }
}

/// <summary>Answers <see cref="ISessionValidator"/> on every authenticated request (G5).</summary>
public sealed class SessionValidator : ISessionValidator
{
    private readonly ITenantScope _tenantScope;
    private readonly ISessionStore _sessions;
    private readonly TimeProvider _time;

    public SessionValidator(ITenantScope tenantScope, ISessionStore sessions, TimeProvider time)
    {
        _tenantScope = tenantScope;
        _sessions = sessions;
        _time = time;
    }

    public async Task<bool> IsActiveAsync(Guid tenantId, Guid sessionId, CancellationToken cancellationToken = default)
    {
        using var scope = _tenantScope.Begin(tenantId);
        var session = await _sessions.FindAsync(sessionId, cancellationToken);
        return session is not null && session.IsActive(_time.GetUtcNow());
    }
}
