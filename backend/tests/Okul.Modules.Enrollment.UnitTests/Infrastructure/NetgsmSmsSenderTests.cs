using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Okul.Modules.Enrollment.Domain;
using Okul.Modules.Enrollment.Infrastructure.Sms;
using Shouldly;

namespace Okul.Modules.Enrollment.UnitTests.Infrastructure;

/// <summary>Spec 0007 — Requirement 5 (G-4), AC-8: 3 attempts within the budget; a fake HTTP handler (plan 0007, R-5).</summary>
public sealed class NetgsmSmsSenderTests
{
    private static readonly PhoneNumber Phone = PhoneNumber.Parse("0532 123 45 67")!;
    private static readonly NetgsmOptions Options = new("kullanici", "parola", "OKUL");

    [Fact]
    public async Task Success_SendsOneJsonRequest_WithBasicAuthTheNationalNumberAndTheCode()
    {
        var handler = new ScriptedHandler(_ => Json("""{"code":"00","jobid":"1"}"""));
        var sender = new NetgsmSmsSender(new HttpClient(handler), Options);

        (await sender.SendLoginCodeAsync(Phone, "482913", TestContext.Current.CancellationToken)).ShouldBeTrue();

        var request = handler.Requests.ShouldHaveSingleItem();
        request.Uri.ShouldBe(NetgsmOptions.Endpoint);
        request.Authorization.ShouldBe("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("kullanici:parola")));
        using var body = JsonDocument.Parse(request.Body);
        body.RootElement.GetProperty("msgheader").GetString().ShouldBe("OKUL");
        var message = body.RootElement.GetProperty("messages")[0];
        message.GetProperty("no").GetString().ShouldBe("5321234567");
        message.GetProperty("msg").GetString()!.ShouldContain("482913");
    }

    [Fact]
    public async Task ProviderError_IsRetried_UpToThreeAttempts_ThenFails()
    {
        var handler = new ScriptedHandler(_ => Json("""{"code":"30"}"""));
        var sender = new NetgsmSmsSender(new HttpClient(handler), Options);

        (await sender.SendLoginCodeAsync(Phone, "482913", TestContext.Current.CancellationToken)).ShouldBeFalse();

        handler.Requests.Count.ShouldBe(NetgsmSmsSender.MaxAttempts);
    }

    [Fact]
    public async Task OneFailure_ThenSuccess_IsASuccess()
    {
        var handler = new ScriptedHandler(n => n == 1 ? new HttpResponseMessage(HttpStatusCode.BadGateway) : Json("""{"code":"00"}"""));
        var sender = new NetgsmSmsSender(new HttpClient(handler), Options);

        (await sender.SendLoginCodeAsync(Phone, "482913", TestContext.Current.CancellationToken)).ShouldBeTrue();

        handler.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task AHangingProvider_GivesUpWithinTheBudget()
    {
        var handler = new ScriptedHandler(_ => throw new InvalidOperationException("never answered"), hang: true);
        var budget = TimeSpan.FromMilliseconds(600);
        var sender = new NetgsmSmsSender(new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan }, Options, budget);
        var watch = Stopwatch.StartNew();

        (await sender.SendLoginCodeAsync(Phone, "482913", TestContext.Current.CancellationToken)).ShouldBeFalse();

        // The property under test: the budget holds and attempts never exceed 3 (how many start depends on timing).
        watch.Elapsed.ShouldBeLessThan(budget + TimeSpan.FromSeconds(1));
        handler.Requests.Count.ShouldBeInRange(1, NetgsmSmsSender.MaxAttempts);
        NetgsmSmsSender.DefaultBudget.ShouldBe(TimeSpan.FromSeconds(10));
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class ScriptedHandler(Func<int, HttpResponseMessage> respond, bool hang = false) : HttpMessageHandler
    {
        public List<(Uri? Uri, string? Authorization, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            lock (Requests)
            {
                Requests.Add((request.RequestUri, request.Headers.Authorization?.ToString(), body));
            }

            if (hang)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            return respond(Requests.Count);
        }
    }
}
