using NArchitecture.Core.CrossCuttingConcerns.Exception.Types;
using NArchitecture.Core.Mediator;
using Okul.Core.Tenancy;
using Okul.Modules.Institution.Contracts;

namespace Okul.Modules.Institution.Application.Queries;

/// <summary>The current user's own kurum. There is deliberately no tenant parameter (AC-3).</summary>
public sealed record GetMyInstitutionQuery : IRequest<InstitutionDto>;

public sealed class GetMyInstitutionQueryHandler : IRequestHandler<GetMyInstitutionQuery, InstitutionDto>
{
    public const string NotFoundMessage = "Kurum bulunamadı.";

    private readonly ITenantContext _tenantContext;
    private readonly IInstitutionReadStore _store;

    public GetMyInstitutionQueryHandler(ITenantContext tenantContext, IInstitutionReadStore store)
    {
        _tenantContext = tenantContext;
        _store = store;
    }

    public async Task<InstitutionDto> Handle(GetMyInstitutionQuery request, CancellationToken cancellationToken)
    {
        TenantGuard.EnsureTenant(_tenantContext);

        return await _store.GetCurrentInstitutionAsync(cancellationToken)
            ?? throw new NotFoundException(NotFoundMessage);
    }
}
