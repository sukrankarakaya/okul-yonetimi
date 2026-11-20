using NArchitecture.Core.Mediator;
using Okul.Core.Tenancy;
using Okul.Modules.Enrollment.Domain;

namespace Okul.Modules.Enrollment.Application;

/// <summary>What the KVKK screen shows (spec 0007, Requirement 6): the text, its version, and whether it is accepted.</summary>
public sealed record GuardianConsentStatus(bool Accepted, string TextVersion, string Text);

/// <summary>The veli and kurum come from the validated session, never from client input.</summary>
public sealed record GetGuardianConsentQuery(Guid TenantId, Guid GuardianId) : IRequest<GuardianConsentStatus>;

public sealed class GetGuardianConsentQueryHandler : IRequestHandler<GetGuardianConsentQuery, GuardianConsentStatus>
{
    private readonly ITenantScope _tenantScope;
    private readonly IKvkkConsentStore _consents;
    private readonly KvkkText _text;

    public GetGuardianConsentQueryHandler(ITenantScope tenantScope, IKvkkConsentStore consents, KvkkText text)
    {
        _tenantScope = tenantScope;
        _consents = consents;
        _text = text;
    }

    public async Task<GuardianConsentStatus> Handle(GetGuardianConsentQuery request, CancellationToken cancellationToken)
    {
        using var scope = _tenantScope.Begin(request.TenantId);
        var latest = await _consents.FindLatestAsync(request.GuardianId, cancellationToken);
        return new GuardianConsentStatus(latest?.Covers(_text.Version) == true, _text.Version, _text.Body);
    }
}

/// <summary>"Devam et" with the box ticked: a rıza record for the CURRENT text version (veli, kurum, sürüm, zaman).</summary>
public sealed record AcceptGuardianConsentCommand(Guid TenantId, Guid GuardianId) : IRequest<GuardianConsentStatus>;

public sealed class AcceptGuardianConsentCommandHandler : IRequestHandler<AcceptGuardianConsentCommand, GuardianConsentStatus>
{
    private readonly ITenantScope _tenantScope;
    private readonly IGuardianStore _guardians;
    private readonly IKvkkConsentStore _consents;
    private readonly KvkkText _text;
    private readonly TimeProvider _time;

    public AcceptGuardianConsentCommandHandler(
        ITenantScope tenantScope, IGuardianStore guardians, IKvkkConsentStore consents, KvkkText text, TimeProvider time)
    {
        _tenantScope = tenantScope;
        _guardians = guardians;
        _consents = consents;
        _text = text;
        _time = time;
    }

    public async Task<GuardianConsentStatus> Handle(AcceptGuardianConsentCommand request, CancellationToken cancellationToken)
    {
        using var scope = _tenantScope.Begin(request.TenantId);
        if (await _guardians.FindByIdAsync(request.GuardianId, cancellationToken) is null)
        {
            throw new InvalidOperationException("Oturumun velisi bu kurumda yok.");
        }

        var latest = await _consents.FindLatestAsync(request.GuardianId, cancellationToken);
        if (latest?.Covers(_text.Version) != true)
        {
            await _consents.AddAsync(
                KvkkConsent.Accept(Guid.CreateVersion7(), request.TenantId, request.GuardianId, _text.Version, _time.GetUtcNow()),
                cancellationToken);
        }

        return new GuardianConsentStatus(true, _text.Version, _text.Body);
    }
}
