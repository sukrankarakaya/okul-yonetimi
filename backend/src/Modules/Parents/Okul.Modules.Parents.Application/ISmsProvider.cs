namespace Okul.Modules.Parents.Application;

public interface ISmsProvider
{
    /// <summary>Send a 6-digit SMS code to the phone number. Max 3 retries on failure.</summary>
    Task<SmsResult> SendCodeAsync(string phoneNumber, string code, CancellationToken cancellationToken);
}

public sealed record SmsResult(bool Success, string? ErrorMessage = null);
