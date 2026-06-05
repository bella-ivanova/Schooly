namespace StudyAssistant.Services;

public class RateLimiter
{
    private const int LoginMaxAttempts = 5;
    private static readonly TimeSpan LoginLockoutDuration    = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan PasswordResetCooldown   = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RegistrationWindow      = TimeSpan.FromMinutes(10);
    private const int RegistrationMaxPerWindow = 3;

    // username/email → (consecutiveFailures, lockedUntil)
    private readonly Dictionary<string, (int Count, DateTime? LockedUntil)> _loginAttempts
        = new(StringComparer.OrdinalIgnoreCase);

    // email → last reset request time
    private readonly Dictionary<string, DateTime> _passwordResetRequests
        = new(StringComparer.OrdinalIgnoreCase);

    // timestamps of recent registrations for sliding-window check
    private readonly Queue<DateTime> _registrationTimestamps = new();

    // ── Login ────────────────────────────────────────────────────────────────

    public bool IsLoginLocked(string usernameOrEmail, out TimeSpan remaining)
    {
        remaining = TimeSpan.Zero;
        if (!_loginAttempts.TryGetValue(usernameOrEmail, out var entry) || entry.LockedUntil is null)
            return false;

        if (DateTime.UtcNow < entry.LockedUntil)
        {
            remaining = entry.LockedUntil.Value - DateTime.UtcNow;
            return true;
        }

        _loginAttempts[usernameOrEmail] = (0, null);
        return false;
    }

    public void RecordLoginFailure(string usernameOrEmail)
    {
        _loginAttempts.TryGetValue(usernameOrEmail, out var entry);
        var newCount = entry.Count + 1;
        var lockedUntil = newCount >= LoginMaxAttempts ? DateTime.UtcNow.Add(LoginLockoutDuration) : (DateTime?)null;
        _loginAttempts[usernameOrEmail] = (newCount, lockedUntil);
    }

    public void RecordLoginSuccess(string usernameOrEmail) =>
        _loginAttempts.Remove(usernameOrEmail);

    public int RemainingLoginAttempts(string usernameOrEmail)
    {
        _loginAttempts.TryGetValue(usernameOrEmail, out var entry);
        return Math.Max(0, LoginMaxAttempts - entry.Count);
    }

    // ── Password reset ───────────────────────────────────────────────────────

    public bool IsPasswordResetThrottled(string email, out TimeSpan remaining)
    {
        remaining = TimeSpan.Zero;
        if (!_passwordResetRequests.TryGetValue(email, out var lastRequest)) return false;
        var elapsed = DateTime.UtcNow - lastRequest;
        if (elapsed < PasswordResetCooldown)
        {
            remaining = PasswordResetCooldown - elapsed;
            return true;
        }
        return false;
    }

    public void RecordPasswordResetRequest(string email) =>
        _passwordResetRequests[email] = DateTime.UtcNow;

    // ── Registration ─────────────────────────────────────────────────────────

    public bool IsRegistrationThrottled(out TimeSpan remaining)
    {
        remaining = TimeSpan.Zero;
        PruneOldRegistrations();

        if (_registrationTimestamps.Count >= RegistrationMaxPerWindow)
        {
            remaining = RegistrationWindow - (DateTime.UtcNow - _registrationTimestamps.Peek());
            return true;
        }
        return false;
    }

    public void RecordRegistration()
    {
        PruneOldRegistrations();
        _registrationTimestamps.Enqueue(DateTime.UtcNow);
    }

    private void PruneOldRegistrations()
    {
        var cutoff = DateTime.UtcNow - RegistrationWindow;
        while (_registrationTimestamps.Count > 0 && _registrationTimestamps.Peek() < cutoff)
            _registrationTimestamps.Dequeue();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    public static string FormatRemaining(TimeSpan remaining) =>
        remaining.TotalSeconds < 90
            ? $"{(int)remaining.TotalSeconds} сек."
            : $"{(int)remaining.TotalMinutes} мин.";
}
