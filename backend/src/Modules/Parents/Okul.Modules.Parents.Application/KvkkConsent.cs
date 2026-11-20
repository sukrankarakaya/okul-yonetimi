using NArchitecture.Core.Mediator;

namespace Okul.Modules.Parents.Application;

public sealed record RecordKvkkConsentCommand(Guid ParentId, Guid TenantId, bool Accepted)
    : IRequest<RecordKvkkConsentResult>;

public sealed record RecordKvkkConsentResult(bool Success, string? ErrorMessage = null);

public sealed class KvkkConsentServices
{
    private readonly IParentAccountStore _accountStore;
    private readonly IKvkkConsentStore _consentStore;

    public KvkkConsentServices(IParentAccountStore accountStore, IKvkkConsentStore consentStore)
    {
        _accountStore = accountStore;
        _consentStore = consentStore;
    }

    public async Task<RecordKvkkConsentResult> RecordConsentAsync(
        Guid parentId, Guid tenantId, bool accepted, CancellationToken cancellationToken)
    {
        var parent = await _accountStore.FindByIdAsync(parentId, cancellationToken);
        if (parent is null || parent.TenantId != tenantId)
        {
            return new RecordKvkkConsentResult(Success: false, ErrorMessage: "Veli hesabı bulunamadı.");
        }

        var consent = Domain.KvkkConsent.Create(parentId, tenantId, accepted);
        await _consentStore.AddAsync(consent, cancellationToken);

        return new RecordKvkkConsentResult(Success: true);
    }
}

public sealed class RecordKvkkConsentCommandHandler : IRequestHandler<RecordKvkkConsentCommand, RecordKvkkConsentResult>
{
    private readonly KvkkConsentServices _services;

    public RecordKvkkConsentCommandHandler(KvkkConsentServices services) => _services = services;

    public Task<RecordKvkkConsentResult> Handle(RecordKvkkConsentCommand request, CancellationToken cancellationToken) =>
        _services.RecordConsentAsync(request.ParentId, request.TenantId, request.Accepted, cancellationToken);
}
