using System.Security.Claims;
using Okul.Modules.School.Application;
using Okul.WebApi.Auth;

namespace Okul.WebApi.Endpoints;

/// <summary>Demo okul paneli uçları (AGENTS.md, Demo modu). Kurum yalnızca oturumdan gelir (BR-1).</summary>
public static class SchoolEndpoints
{
    public static void MapSchoolEndpoints(this IEndpointRouteBuilder api)
    {
        var school = api.MapGroup("/school").WithTags("School");

        school.MapGet("/dashboard", (ISchoolService s, CancellationToken ct) => s.GetDashboardAsync(ct));
        school.MapGet("/classes", (ISchoolService s, CancellationToken ct) => s.GetClassesAsync(ct));
        school.MapGet("/students", (Guid? classId, string? q, ISchoolService s, CancellationToken ct) => s.GetStudentsAsync(classId, q, ct));
        school.MapGet("/students/{id:guid}", async (Guid id, ISchoolService s, CancellationToken ct) =>
            await s.GetStudentAsync(id, ct) is { } dto ? Results.Ok(dto) : NotFound("Öğrenci bulunamadı."));

        school.MapGet("/attendance", async (Guid classId, DateOnly date, ISchoolService s, CancellationToken ct) =>
            await s.GetAttendanceAsync(classId, date, ct) is { } dto ? Results.Ok(dto) : NotFound("Sınıf bulunamadı."));
        school.MapPost("/attendance", (SaveAttendanceRequest body, ISchoolService s, CancellationToken ct) => s.SaveAttendanceAsync(body, ct));

        school.MapGet("/announcements", (ISchoolService s, CancellationToken ct) => s.GetAnnouncementsAsync(ct));
        school.MapPost("/announcements", (CreateAnnouncementRequest body, ISchoolService s, CancellationToken ct) => s.CreateAnnouncementAsync(body, ct));

        school.MapGet("/installments", (string? filter, ISchoolService s, CancellationToken ct) => s.GetInstallmentsAsync(filter, ct));
        school.MapPost("/installments/{id:guid}/pay", async (Guid id, ISchoolService s, CancellationToken ct) =>
            await s.PayInstallmentAsync(id, ct) is { } dto ? Results.Ok(dto) : NotFound("Taksit bulunamadı."));

        school.MapPost("/messages/draft", async (DraftMessageRequest body, ISchoolService s, CancellationToken ct) =>
            await s.DraftMessageAsync(body, ct) is { } dto ? Results.Ok(dto) : NotFound("Öğrenci bulunamadı."));
        school.MapPost("/messages", async (SendMessageRequest body, ClaimsPrincipal user, ISchoolService s, CancellationToken ct) =>
            await s.SendMessageAsync(body, SenderLabel(user), ct) is { } dto ? Results.Ok(dto) : NotFound("Öğrenci bulunamadı."));
        school.MapGet("/messages", (ISchoolService s, CancellationToken ct) => s.GetMessagesAsync(ct));
    }

    private static IResult NotFound(string detail) =>
        Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Bulunamadı", detail: detail);

    private static string SenderLabel(ClaimsPrincipal user) =>
        user.IsInRole(Roles.Teacher) ? "Öğretmen"
        : user.IsInRole(Roles.Administrator) ? "Yönetici"
        : user.IsInRole(Roles.Accountant) ? "Muhasebe"
        : "Personel";
}
