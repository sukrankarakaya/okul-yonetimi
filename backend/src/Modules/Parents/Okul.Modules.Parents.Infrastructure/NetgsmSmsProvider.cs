using Okul.Modules.Parents.Application;

namespace Okul.Modules.Parents.Infrastructure;

public interface INetgsmConfig
{
    string ApiKey { get; }
    string SenderId { get; }
}

public sealed class NetgsmSmsProvider : ISmsProvider
{
    private readonly INetgsmConfig _config;
    private readonly HttpClient _httpClient;
    private const int MaxRetries = 3;
    private const int RetryDelayMs = 1000;

    public NetgsmSmsProvider(INetgsmConfig config, HttpClient httpClient)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<SmsResult> SendCodeAsync(string phoneNumber, string code, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phoneNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                var result = await SendToNetgsmAsync(phoneNumber, code, cancellationToken);
                if (result.Success)
                {
                    return result;
                }

                if (attempt < MaxRetries)
                {
                    await Task.Delay(RetryDelayMs * attempt, cancellationToken);
                }
            }
            catch (Exception) when (attempt < MaxRetries)
            {
                await Task.Delay(RetryDelayMs * attempt, cancellationToken);
            }
        }

        return new SmsResult(Success: false, ErrorMessage: "SMS send failed after retries");
    }

    private async Task<SmsResult> SendToNetgsmAsync(string phoneNumber, string code, CancellationToken cancellationToken)
    {
        const string netgsmUrl = "https://api.netgsm.com.tr/sms/send/get/";

        var requestParams = new Dictionary<string, string>
        {
            { "usercode", _config.ApiKey },
            { "password", _config.ApiKey }, // placeholder; real Netgsm uses apikey for auth
            { "gsmno", phoneNumber },
            { "message", $"Doğrulama kodunuz: {code}" },
            { "msgheader", _config.SenderId }
        };

        var content = new FormUrlEncodedContent(requestParams);
        var response = await _httpClient.PostAsync(netgsmUrl, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new SmsResult(Success: false, ErrorMessage: $"HTTP {response.StatusCode}");
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        // Netgsm returns "00" on success, error codes otherwise
        var success = responseBody.StartsWith("00");
        return new SmsResult(Success: success, ErrorMessage: success ? null : $"Netgsm error: {responseBody}");
    }
}
