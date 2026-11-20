using System.Collections.Concurrent;
using Okul.Modules.Enrollment.Application;
using Okul.Modules.Enrollment.Domain;

namespace Okul.Modules.Enrollment.Infrastructure.Sms;

/// <summary>
/// Development and Testing only (spec 0007, Requirement 5): nothing leaves the machine. Messages are kept in memory for
/// the tests; nothing is written to a log (Requirement 11). Never registered in Production (the API refuses to start).
/// </summary>
public sealed class FakeSmsSender : ISmsSender
{
    private readonly ConcurrentQueue<SentSms> _sent = new();

    /// <summary>Tests: make the next sends fail, as a provider that is down after all retries.</summary>
    public bool Fail { get; set; }

    public IReadOnlyList<SentSms> Sent => [.. _sent];

    public Task<bool> SendLoginCodeAsync(PhoneNumber phone, string code, CancellationToken cancellationToken)
    {
        if (Fail)
        {
            return Task.FromResult(false);
        }

        _sent.Enqueue(new SentSms(phone.E164, code));
        return Task.FromResult(true);
    }

    /// <summary>The last code sent to <paramref name="e164"/>, if any.</summary>
    public string? LastCodeFor(string e164) => _sent.LastOrDefault(s => s.Phone == e164)?.Code;

    public sealed record SentSms(string Phone, string Code);
}
