using NArchitecture.Core.Mediator;
using Okul.Modules.Institution.Application.Queries;
using Okul.Modules.Institution.Contracts;
using Okul.WebApi.Auth;

namespace Okul.WebApi.Endpoints;

public static class InstitutionEndpoints
{
    public static void MapInstitutionEndpoints(this IEndpointRouteBuilder api)
    {
        // No tenant parameter anywhere: the kurum is the authenticated user's own (AC-3).
        api.MapGet("/institution/me", async (ISender sender, CancellationToken cancellationToken) =>
                TypedResults.Ok(await sender.Send(new GetMyInstitutionQuery(), cancellationToken)))
            // Spec 0001: every role reads its own kurum — the veli too (spec 0007 flow: Kurum bilgisi until 0006).
            .RequireAuthorization(AuthEndpoints.AnySessionPolicy)
            .WithName("GetMyInstitution")
            .WithTags("Institution")
            .Produces<InstitutionDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapGet("/campuses/{campusId:guid}", async (Guid campusId, ISender sender, CancellationToken cancellationToken) =>
                TypedResults.Ok(await sender.Send(new GetCampusByIdQuery(campusId), cancellationToken)))
            .RequireAuthorization(AuthEndpoints.AnySessionPolicy)
            .WithName("GetCampus")
            .WithTags("Institution")
            .Produces<CampusDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
