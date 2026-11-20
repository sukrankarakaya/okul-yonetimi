using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using Okul.Modules.Enrollment.Application;
using Okul.Modules.Enrollment.Domain;

namespace Okul.Modules.Enrollment.Infrastructure.Sms;

/// <summary>Netgsm account (<c>Sms:Netgsm:*</c>, secret manager only — docs/security.md).</summary>
public sealed record NetgsmOptions(string UserCode, string Password, string SenderId)
{
    public static readonly Uri Endpoint = new("https://api.netgsm.com.tr/sms/rest/v2/send");
}

/// <summary>
/// Production SMS through Netgsm's REST API (spec 0007, Requirement 5): 3 attempts, all within 10 seconds (G-4); a
/// non-"00" answer, an HTTP error or a timeout is a failed attempt. Nothing is logged — the message holds the code
/// (Requirement 11). Proven against a fake HTTP handler only; a real send needs the account (plan 0007, R-5).
/// </summary>
public sealed class NetgsmSmsSender : ISmsSender
{
    public const int MaxAttempts = 3;
    public static readonly TimeSpan DefaultBudget = TimeSpan.FromSeconds(10);

    private readonly HttpClient _http;
    private readonly NetgsmOptions _options;
    private readonly TimeSpan _budget;

    public NetgsmSmsSender(HttpClient http, NetgsmOptions options, TimeSpan? budget = null)
    {
        _http = http;
        _options = options;
        _budget = budget ?? DefaultBudget;
    }

    public async Task<bool> SendLoginCodeAsync(PhoneNumber phone, string code, CancellationToken cancellationToken)
    {
        using var total = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        total.CancelAfter(_budget);
        var perAttempt = _budget / MaxAttempts;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            if (total.IsCancellationRequested)
            {
                break;
            }

            using var single = CancellationTokenSource.CreateLinkedTokenSource(total.Token);
            single.CancelAfter(perAttempt);
            try
            {
                if (await SendOnceAsync(phone, code, single.Token))
                {
                    return true;
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // this attempt timed out — try again while the budget lasts
            }
            catch (HttpRequestException)
            {
                // network error — try again while the budget lasts
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        return false;
    }

    private async Task<bool> SendOnceAsync(PhoneNumber phone, string code, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, NetgsmOptions.Endpoint)
        {
            Content = JsonContent.Create(new NetgsmRequest(
                _options.SenderId,
                [new NetgsmMessage($"Okul giriş kodunuz: {code}. Kod 5 dakika geçerlidir.", phone.National)],
                "TR")),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.UserCode}:{_options.Password}")));

        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        var body = await response.Content.ReadFromJsonAsync<NetgsmResponse>(cancellationToken);
        return body?.Code == "00";
    }

    private sealed record NetgsmRequest(
        [property: JsonPropertyName("msgheader")] string Header,
        [property: JsonPropertyName("messages")] IReadOnlyList<NetgsmMessage> Messages,
        [property: JsonPropertyName("encoding")] string Encoding);

    private sealed record NetgsmMessage(
        [property: JsonPropertyName("msg")] string Text,
        [property: JsonPropertyName("no")] string Number);

    private sealed record NetgsmResponse([property: JsonPropertyName("code")] string? Code);
}
