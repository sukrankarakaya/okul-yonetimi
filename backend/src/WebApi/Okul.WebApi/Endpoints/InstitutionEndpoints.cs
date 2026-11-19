using NArchitecture.Core.Mediator;
using Okul.Modules.Institution.Application.Queries;
using Okul.Modules.Institution.Contracts;

namespace Okul.WebApi.Endpoints;

public static class InstitutionEndpoints
{
    public static void MapInstitutionEndpoints(this IEndpointRouteBuilder api)
    {
        // No tenant parameter anywhere: the kurum is the authenticated user's own (AC-3).
        api.MapGet("/institution/me", async (ISender sender, CancellationToken cancellationToken) =>
                TypedResults.Ok(await sender.Send(new GetMyInstitutionQuery(), cancellationToken)))
            .WithName("GetMyInstitution")
            .WithTags("Institution")
            .Produces<InstitutionDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapGet("/campuses/{campusId:guid}", async (Guid campusId, ISender sender, CancellationToken cancellationToken) =>
                TypedResults.Ok(await sender.Send(new GetCampusByIdQuery(campusId), cancellationToken)))
            .WithName("GetCampus")
            .WithTags("Institution")
            .Produces<CampusDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
