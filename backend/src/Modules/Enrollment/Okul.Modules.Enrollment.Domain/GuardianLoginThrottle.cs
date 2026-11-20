namespace Okul.Modules.Enrollment.Domain;

/// <summary>
/// Failed codes and code requests of one kurum kodu + telefon pair (spec 0007, Requirement 4). Keyed by the pair, not by a
/// veli, so an unregistered number locks exactly like a registered one (G-3). Failure rules are spec 0003's
/// (Requirement 4): 5 consecutive failures lock for 15 minutes; "consecutive" ends 15 minutes after the last failure;
/// failures while locked do not extend the lock; a successful login clears them. At most 3 code requests a minute,
/// "tekrar gönder" included (G-2).
/// </summary>
public sealed class GuardianLoginThrottle
{
    public const int MaxFailures = 5;
    public const int MaxCodeRequests = 3;
    public static readonly TimeSpan FailureWindow = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan RequestWindow = TimeSpan.FromMinutes(1);

    private GuardianLoginThrottle()
    {
    }

    /// <summary>Keyed hash of the normalized pair — the telefon of an unregistered number is never stored in clear.</summary>
    public string Key { get; private set; } = string.Empty;

    public int FailureCount { get; private set; }

    public DateTimeOffset? LastFailureAt { get; private set; }

    public DateTimeOffset? LockedUntil { get; private set; }

    public DateTimeOffset? RequestWindowStartedAt { get; private set; }

    public int RequestCount { get; private set; }

    public static GuardianLoginThrottle Create(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Anahtar boş olamaz.", nameof(key));
        }

        return new GuardianLoginThrottle { Key = key };
    }

    public bool IsLocked(DateTimeOffset now) => LockedUntil is { } until && now < until;

    /// <summary>Records a wrong code. Returns true when this failure locked the pair.</summary>
    public bool RegisterFailure(DateTimeOffset now)
    {
        if (IsLocked(now))
        {
            return false;
        }

        if (LastFailureAt is { } last && now - last >= FailureWindow)
        {
            FailureCount = 0;
        }

        LockedUntil = null;
        FailureCount++;
        LastFailureAt = now;

        if (FailureCount < MaxFailures)
        {
            return false;
        }

        LockedUntil = now + LockDuration;
        FailureCount = 0;
        return true;
    }

    /// <summary>Counts a code request; false when this would be the 4th in the current minute (nothing is counted then).</summary>
    public bool TryRegisterCodeRequest(DateTimeOffset now)
    {
        if (RequestWindowStartedAt is not { } start || now - start >= RequestWindow)
        {
            RequestWindowStartedAt = now;
            RequestCount = 0;
        }

        if (RequestCount >= MaxCodeRequests)
        {
            return false;
        }

        RequestCount++;
        return true;
    }

    /// <summary>A successful login clears the failures (not the request window).</summary>
    public void ResetFailures()
    {
        FailureCount = 0;
        LastFailureAt = null;
        LockedUntil = null;
    }
}
