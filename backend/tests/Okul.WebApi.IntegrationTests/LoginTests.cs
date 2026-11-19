using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Okul.Modules.Identity.Infrastructure.Persistence;
using Okul.Modules.Institution.Contracts;
using Okul.Modules.Institution.Infrastructure.Persistence;
using Okul.WebApi.Auth;
using Okul.WebApi.IntegrationTests.Infrastructure;
using Shouldly;

namespace Okul.WebApi.IntegrationTests;

/// <summary>Spec 0003 — Requirements 1–3. Sensitive area: Kimlik ve yetki, Tenant izolasyonu.</summary>
[Trait("Category", "Integration")]
public sealed class LoginTests
{
    private const string LoginRoute = "/api/v1/auth/login";
    private const string MineRoute = "/api/v1/institution/me";
    private readonly ApiFixture _api;

    public LoginTests(ApiFixture api)
    {
        _api = api;
    }

    public static TheoryData<string> StaffAccounts() =>
        new(nameof(IdentitySampleData.Administrator), nameof(IdentitySampleData.Teacher), nameof(IdentitySampleData.Accountant));

    /// <summary>AC-1 — Yönetici, Öğretmen and Muhasebe each log in, web and mobile, and see their own kurum.</summary>
    [Theory]
    [MemberData(nameof(StaffAccounts))]
    public async Task EachStaffRole_LogsIn_SeesOwnInstitution(string accountName)
    {
        var ct = TestContext.Current.CancellationToken;
        var account = Sample(accountName);

        var (mobile, session) = await _api.LoginMobileAsync(account);
        var web = await _api.LoginWebAsync(account);

        session.Roles.ShouldBe(account.Roles);
        foreach (var client in new[] { mobile, web })
        {
            var institution = await client.GetFromJsonAsync<InstitutionDto>(MineRoute, ct);
            institution!.Id.ShouldBe(InstitutionSampleData.WithCampusesId);
            institution.Name.ShouldBe(InstitutionSampleData.WithCampusesName);
        }
    }

    /// <summary>AC-1 — a two-role account's session carries both roles.</summary>
    [Fact]
    public async Task TwoRoleAccount_SessionCarriesBothRoles()
    {
        var ct = TestContext.Current.CancellationToken;
        var (client, session) = await _api.LoginMobileAsync(IdentitySampleData.TeacherAndAccountant);

        session.Roles.ShouldBe(["Teacher", "Accountant"]);
        var info = await client.GetFromJsonAsync<SessionInfo>("/api/v1/auth/session", ct);
        info!.Roles.ShouldBe(["Teacher", "Accountant"], ignoreOrder: true);
    }

    /// <summary>
    /// AC-2 — wrong password, unknown e-mail, unknown kurum kodu, another kurum's e-mail and a guardian-only account:
    /// one answer, no session, no cookie.
    /// </summary>
    [Fact]
    public async Task WrongInputs_AllGetTheSameAnswer_NoSession()
    {
        var ct = TestContext.Current.CancellationToken;
        var wrongAccount = await _api.CreateAccountAsync(InstitutionSampleData.WithCampusesId);
        var otherKurumOnly = await _api.CreateAccountAsync(InstitutionSampleData.WithoutCampusesId);
        LoginRequest[] attempts =
        [
            new(InstitutionSampleData.WithCampusesCode, wrongAccount.Email, "yanlis-sifre-123", "web"),
            new(InstitutionSampleData.WithCampusesCode, $"yok-{Guid.NewGuid():N}@okul.test", _api.SamplePassword, "web"),
            new("YOK-KOD", wrongAccount.Email, _api.SamplePassword, "web"),
            new(InstitutionSampleData.WithCampusesCode, otherKurumOnly.Email, _api.SamplePassword, "web"),
            new(InstitutionSampleData.WithCampusesCode, IdentitySampleData.GuardianOnly.Email, _api.SamplePassword, "mobile"),
        ];

        var answers = new List<(HttpStatusCode, string?, string?)>();
        foreach (var attempt in attempts)
        {
            var client = _api.WebClient();
            var response = await client.PostAsJsonAsync(LoginRoute, attempt, ct);
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(ct);
            answers.Add((response.StatusCode, problem!.Title, problem.Detail));
            response.Headers.Contains("Set-Cookie").ShouldBeFalse();
            (await client.GetAsync(MineRoute, ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        answers.Distinct().ShouldHaveSingleItem()
            .ShouldBe((HttpStatusCode.Unauthorized, "Giriş başarısız", "Kurum kodu, e-posta veya şifre hatalı."));
    }

    /// <summary>AC-3 — kurum kodu and e-mail are case- and space-insensitive, Turkish I included.</summary>
    [Fact]
    public async Task CodeAndEmail_AreCaseAndSpaceInsensitive()
    {
        var ct = TestContext.Current.CancellationToken;
        var spellings = new[]
        {
            (Code: " ataturk-ıst ", Email: "  YONETİCİ@ATATURK.TEST "),
            (Code: "Ataturk-Ist", Email: "Yonetici@Ataturk.Test"),
        };

        foreach (var (code, email) in spellings)
        {
            var response = await _api.Testing.CreateClient().PostAsJsonAsync(
                LoginRoute, new LoginRequest(code, email, _api.SamplePassword, "mobile"), ct);

            response.StatusCode.ShouldBe(HttpStatusCode.OK, $"{code} / {email}");
        }
    }

    /// <summary>
    /// Regression (found in build step 4): concurrent logins of one pair raced on the throttle row (23505 → 500).
    /// Ten right and ten wrong attempts at once: every answer is the normal one.
    /// </summary>
    [Fact]
    public async Task ConcurrentLogins_ForOnePair_NeverFail()
    {
        var ct = TestContext.Current.CancellationToken;
        var account = await _api.CreateAccountAsync(InstitutionSampleData.WithCampusesId);
        var right = new LoginRequest(InstitutionSampleData.WithCampusesCode, account.Email, _api.SamplePassword, "mobile");
        var wrong = right with { Password = "yanlis-sifre-123" };
        var other = await _api.CreateAccountAsync(InstitutionSampleData.WithCampusesId);
        var wrongOther = wrong with { Email = other.Email };

        var successes = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ =>
            _api.Testing.CreateClient().PostAsJsonAsync(LoginRoute, right, ct)));
        var failures = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ =>
            _api.Testing.CreateClient().PostAsJsonAsync(LoginRoute, wrongOther, ct)));

        successes.Select(r => r.StatusCode).ShouldAllBe(s => s == HttpStatusCode.OK);
        failures.Select(r => r.StatusCode).ShouldAllBe(s => s == HttpStatusCode.Unauthorized);
    }

    /// <summary>AC-4 — the kurum is the login's; nothing the client sends changes it.</summary>
    [Fact]
    public async Task ClientCannotChangeTenant()
    {
        var ct = TestContext.Current.CancellationToken;
        var (client, _) = await _api.LoginMobileAsync(IdentitySampleData.OtherKurumAdministrator);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{MineRoute}?tenantId={InstitutionSampleData.WithCampusesId}");
        request.Headers.Add("X-Tenant-Id", InstitutionSampleData.WithCampusesId.ToString());
        request.Headers.Add("X-Institution-Code", InstitutionSampleData.WithCampusesCode);

        var institution = await (await client.SendAsync(request, ct)).Content.ReadFromJsonAsync<InstitutionDto>(ct);

        institution!.Id.ShouldBe(InstitutionSampleData.WithoutCampusesId);
    }

    private static IdentitySampleData.SampleAccount Sample(string name) => name switch
    {
        nameof(IdentitySampleData.Administrator) => IdentitySampleData.Administrator,
        nameof(IdentitySampleData.Teacher) => IdentitySampleData.Teacher,
        _ => IdentitySampleData.Accountant,
    };
}
