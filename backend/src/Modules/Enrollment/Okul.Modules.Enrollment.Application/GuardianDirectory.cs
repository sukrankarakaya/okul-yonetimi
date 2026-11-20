using Okul.Core.Tenancy;
using Okul.Modules.Enrollment.Contracts;

namespace Okul.Modules.Enrollment.Application;

/// <summary>The aydınlatma metni shown to veliler and its version (spec 0007, Requirement 6; R-7: "taslak" until approved).</summary>
public sealed record KvkkText(string Version, string Body)
{
    public static KvkkText Create(string? version, string? body) => new(
        string.IsNullOrWhiteSpace(version) ? "taslak" : version.Trim(),
        string.IsNullOrWhiteSpace(body) ? "[Aydınlatma metni — hukuk onayı bekliyor]" : body);
}

/// <summary>Answers <see cref="IGuardianDirectory"/> for Kimlik and the API (ADR 0002).</summary>
public sealed class GuardianDirectory : IGuardianDirectory
{
    private readonly ITenantScope _tenantScope;
    private readonly IGuardianStore _guardians;
    private readonly IKvkkConsentStore _consents;
    private readonly KvkkText _text;

    public GuardianDirectory(ITenantScope tenantScope, IGuardianStore guardians, IKvkkConsentStore consents, KvkkText text)
    {
        _tenantScope = tenantScope;
        _guardians = guardians;
        _consents = consents;
        _text = text;
    }

    public async Task<bool> IsActiveAsync(Guid tenantId, Guid guardianId, CancellationToken cancellationToken = default)
    {
        using var scope = _tenantScope.Begin(tenantId);
        var guardian = await _guardians.FindByIdAsync(guardianId, cancellationToken);
        return guardian is { HasAccount: true };
    }

    public async Task<bool> HasCurrentConsentAsync(Guid tenantId, Guid guardianId, CancellationToken cancellationToken = default)
    {
        using var scope = _tenantScope.Begin(tenantId);
        var latest = await _consents.FindLatestAsync(guardianId, cancellationToken);
        return latest is not null && latest.Covers(_text.Version);
    }
}
