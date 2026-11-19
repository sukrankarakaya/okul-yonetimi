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
        await _sessions.SaveChangesAsync(cancellationToken);

        switch (outcome)
        {
            case RefreshOutcome.ParallelWithinGrace:
                return new RefreshResult(RefreshStatus.Conflict);
            case RefreshOutcome.Reused or RefreshOutcome.Ended:
                return new RefreshResult(RefreshStatus.Rejected);
        }

        var account = await _accounts.FindByIdAsync(session.AccountId, cancellationToken);
        if (account is null || !account.CanLogInAsStaff)
        {
            session.Close(now);
            await _sessions.SaveChangesAsync(cancellationToken);
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
        var session = await _sessions.FindAsync(request.SessionId, cancellationToken);
        if (session is null)
        {
            return false;
        }

        session.Close(_time.GetUtcNow());
        await _sessions.SaveChangesAsync(cancellationToken);
        return true;
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
