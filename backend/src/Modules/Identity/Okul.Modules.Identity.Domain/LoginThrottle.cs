namespace Okul.Modules.Identity.Domain;

/// <summary>
/// Failed-login state of one kurum kodu + e-posta pair (spec 0003, Requirement 4; plan 0003, R4). Keyed by the pair,
/// not by an account, so an unknown account locks exactly like a real one. Rules:
/// 5 consecutive failures lock for 15 minutes; "consecutive" ends 15 minutes after the last failure; failures while
/// locked do not extend the lock; a successful login clears the state.
/// </summary>
public sealed class LoginThrottle
{
    public const int MaxFailures = 5;
    public static readonly TimeSpan FailureWindow = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);

    private LoginThrottle()
    {
    }

    /// <summary>A hash of the normalized pair — the e-mail of an unknown account is never stored in clear.</summary>
    public string Key { get; private set; } = string.Empty;

    public int FailureCount { get; private set; }

    public DateTimeOffset? LastFailureAt { get; private set; }

    public DateTimeOffset? LockedUntil { get; private set; }

    public static LoginThrottle Create(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Anahtar boş olamaz.", nameof(key));
        }

        return new LoginThrottle { Key = key };
    }

    public bool IsLocked(DateTimeOffset now) => LockedUntil is { } until && now < until;

    /// <summary>Records a failed attempt. Returns true when this failure locked the pair.</summary>
    public bool RegisterFailure(DateTimeOffset now)
    {
        if (IsLocked(now))
        {
            return false; // a locked pair stays locked until LockedUntil — attempts never extend it
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

    public void Reset()
    {
        FailureCount = 0;
        LastFailureAt = null;
        LockedUntil = null;
    }
}
