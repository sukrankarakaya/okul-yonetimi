using NArchitecture.Core.CrossCuttingConcerns.Exception.Types;
using NSubstitute;
using Okul.Core.Tenancy;
using Okul.Modules.Institution.Application;
using Okul.Modules.Institution.Application.Queries;
using Okul.Modules.Institution.Contracts;
using Shouldly;

namespace Okul.Modules.Institution.UnitTests.Application;

public sealed class QueryHandlerTests
{
    private static readonly Guid Tenant = Guid.CreateVersion7();
    private readonly IInstitutionReadStore _store = Substitute.For<IInstitutionReadStore>();

    private static ITenantContext TenantContext(Guid? tenantId)
    {
        var context = Substitute.For<ITenantContext>();
        context.TenantId.Returns(tenantId);
        return context;
    }

    [Fact]
    public async Task GetMyInstitution_ReturnsStoreResult()
    {
        var dto = new InstitutionDto(Tenant, "Atatürk Koleji", [new CampusDto(Guid.CreateVersion7(), "Üsküdar")]);
        _store.GetCurrentInstitutionAsync(Arg.Any<CancellationToken>()).Returns(dto);
        var handler = new GetMyInstitutionQueryHandler(TenantContext(Tenant), _store);

        var result = await handler.Handle(new GetMyInstitutionQuery(), TestContext.Current.CancellationToken);

        result.ShouldBe(dto);
    }

    [Fact]
    public async Task GetMyInstitution_NotInStore_ThrowsNotFound()
    {
        _store.GetCurrentInstitutionAsync(Arg.Any<CancellationToken>()).Returns((InstitutionDto?)null);
        var handler = new GetMyInstitutionQueryHandler(TenantContext(Tenant), _store);

        var error = await Should.ThrowAsync<NotFoundException>(
            () => handler.Handle(new GetMyInstitutionQuery(), TestContext.Current.CancellationToken));

        error.Message.ShouldBe(GetMyInstitutionQueryHandler.NotFoundMessage);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetMyInstitution_WithoutTenant_FailsWithoutQuerying(bool emptyGuid)
    {
        var handler = new GetMyInstitutionQueryHandler(TenantContext(emptyGuid ? Guid.Empty : null), _store);

        await Should.ThrowAsync<InvalidOperationException>(
            () => handler.Handle(new GetMyInstitutionQuery(), TestContext.Current.CancellationToken));

        await _store.DidNotReceiveWithAnyArgs().GetCurrentInstitutionAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetCampus_ReturnsStoreResult()
    {
        var campus = new CampusDto(Guid.CreateVersion7(), "Şişli");
        _store.GetCampusAsync(campus.Id, Arg.Any<CancellationToken>()).Returns(campus);
        var handler = new GetCampusByIdQueryHandler(TenantContext(Tenant), _store);

        var result = await handler.Handle(new GetCampusByIdQuery(campus.Id), TestContext.Current.CancellationToken);

        result.ShouldBe(campus);
    }

    [Fact]
    public async Task GetCampus_NotInStore_ThrowsNotFound()
    {
        _store.GetCampusAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((CampusDto?)null);
        var handler = new GetCampusByIdQueryHandler(TenantContext(Tenant), _store);

        var error = await Should.ThrowAsync<NotFoundException>(
            () => handler.Handle(new GetCampusByIdQuery(Guid.CreateVersion7()), TestContext.Current.CancellationToken));

        error.Message.ShouldBe(GetCampusByIdQueryHandler.NotFoundMessage);
    }

    [Fact]
    public async Task GetCampus_WithoutTenant_FailsWithoutQuerying()
    {
        var handler = new GetCampusByIdQueryHandler(TenantContext(null), _store);

        await Should.ThrowAsync<InvalidOperationException>(
            () => handler.Handle(new GetCampusByIdQuery(Guid.CreateVersion7()), TestContext.Current.CancellationToken));

        await _store.DidNotReceiveWithAnyArgs().GetCampusAsync(default, TestContext.Current.CancellationToken);
    }
}
