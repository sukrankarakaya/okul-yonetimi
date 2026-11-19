using NArchitecture.Core.CrossCuttingConcerns.Exception.Types;
using NArchitecture.Core.Mediator;
using Okul.Core.Tenancy;
using Okul.Modules.Institution.Contracts;

namespace Okul.Modules.Institution.Application.Queries;

public sealed record GetCampusByIdQuery(Guid CampusId) : IRequest<CampusDto>;

public sealed class GetCampusByIdQueryHandler : IRequestHandler<GetCampusByIdQuery, CampusDto>
{
    /// <summary>Same answer for "another kurum's campus" and "no such campus" (AC-4).</summary>
    public const string NotFoundMessage = "Şube bulunamadı.";

    private readonly ITenantContext _tenantContext;
    private readonly IInstitutionReadStore _store;

    public GetCampusByIdQueryHandler(ITenantContext tenantContext, IInstitutionReadStore store)
    {
        _tenantContext = tenantContext;
        _store = store;
    }

    public async Task<CampusDto> Handle(GetCampusByIdQuery request, CancellationToken cancellationToken)
    {
        TenantGuard.EnsureTenant(_tenantContext);

        return await _store.GetCampusAsync(request.CampusId, cancellationToken)
            ?? throw new NotFoundException(NotFoundMessage);
    }
}
