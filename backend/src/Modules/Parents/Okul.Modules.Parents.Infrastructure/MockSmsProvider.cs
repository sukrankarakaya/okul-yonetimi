using Okul.Modules.Parents.Application;

namespace Okul.Modules.Parents.Infrastructure;

public sealed class MockSmsProvider : ISmsProvider
{
    private readonly Dictionary<string, string> _sentCodes = new();

    public Task<SmsResult> SendCodeAsync(string phoneNumber, string code, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phoneNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        _sentCodes[phoneNumber] = code;
        return Task.FromResult(new SmsResult(Success: true));
    }

    /// <summary>Test helper: retrieve the last sent code for a phone number.</summary>
    public string? GetLastSentCode(string phoneNumber) =>
        _sentCodes.TryGetValue(phoneNumber, out var code) ? code : null;
}
