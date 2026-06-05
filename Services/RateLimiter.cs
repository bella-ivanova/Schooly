using System.Text.Json;
using System.Text.Json.Serialization;

namespace StudyAssistant.Services;

public class RateLimiter
{
    // Progressive delays per consecutive failure (index = failure count, capped at last entry).
    // Replaces the hard 15-min lockout so an attacker cannot permanently deny access to any account.
    private static readonly TimeSpan[] LoginDelays =
    [
        TimeSpan.Zero,            // 1st failure: no wait
        TimeSpan.FromSeconds(3),  // 2nd
        TimeSpan.FromSeconds(10), // 3rd
        TimeSpan.FromSeconds(30), // 4th
        TimeSpan.FromSeconds(60), // 5th and beyond
    ];
    private static readonly TimeSpan PasswordResetCooldown  = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RegistrationCooldown   = TimeSpan.FromMinutes(2);

    private static readonly string StateFile = Path.Combine(
        AppContext.BaseDirectory, "rate_limiter_state.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
        { WriteIndented = false, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    private Dictionary<string, LoginEntry> _loginAttempts
        = new(StringComparer.OrdinalIgnoreCase);

    private Dictionary<string, DateTime> _passwordResetRequests
        = new(StringComparer.OrdinalIgnoreCase);

    // email → last successful registration attempt
    private Dictionary<string, DateTime> _registrationRequests
        = new(StringComparer.OrdinalIgnoreCase);

    public RateLimiter() => Load();

    // ── Login ────────────────────────────────────────────────────────────────

    public bool IsLoginLocked(string usernameOrEmail, out TimeSpan remaining)
    {
        remaining = TimeSpan.Zero;
        if (!_loginAttempts.TryGetValue(usernameOrEmail, out var entry) || entry.NextAttemptAfter is null)
            return false;

        if (DateTime.UtcNow < entry.NextAttemptAfter)
        {
            remaining = entry.NextAttemptAfter.Value - DateTime.UtcNow;
            return true;
        }

        // Delay expired — clear it but keep the failure count so delays keep growing
        _loginAttempts[usernameOrEmail] = entry with { NextAttemptAfter = null };
        Save();
        return false;
    }

    public void RecordLoginFailure(string usernameOrEmail)
    {
        _loginAttempts.TryGetValue(usernameOrEmail, out var entry);
        var newCount    = entry.Count + 1;
        var delay       = LoginDelays[Math.Min(newCount, LoginDelays.Length - 1)];
        var nextAllowed = delay > TimeSpan.Zero ? DateTime.UtcNow.Add(delay) : (DateTime?)null;
        _loginAttempts[usernameOrEmail] = new LoginEntry(newCount, nextAllowed);
        Save();
    }

    public void RecordLoginSuccess(string usernameOrEmail)
    {
        _loginAttempts.Remove(usernameOrEmail);
        Save();
    }

    public int RemainingAttemptsBeforeDelay(string usernameOrEmail)
    {
        _loginAttempts.TryGetValue(usernameOrEmail, out var entry);
        return Math.Max(0, LoginDelays.Length - 1 - entry.Count);
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

    public void RecordPasswordResetRequest(string email)
    {
        _passwordResetRequests[email] = DateTime.UtcNow;
        Save();
    }

    // ── Registration ─────────────────────────────────────────────────────────

    public bool IsRegistrationThrottled(string email, out TimeSpan remaining)
    {
        remaining = TimeSpan.Zero;
        if (!_registrationRequests.TryGetValue(email, out var lastAttempt)) return false;
        var elapsed = DateTime.UtcNow - lastAttempt;
        if (elapsed < RegistrationCooldown)
        {
            remaining = RegistrationCooldown - elapsed;
            return true;
        }
        return false;
    }

    public void RecordRegistration(string email)
    {
        _registrationRequests[email] = DateTime.UtcNow;
        Save();
    }

    // ── Persistence ──────────────────────────────────────────────────────────

    private void Save()
    {
        try
        {
            var now = DateTime.UtcNow;

            var activeLogins = _loginAttempts
                .Where(kv => kv.Value.Count > 0)
                .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);

            var activeResets = _passwordResetRequests
                .Where(kv => now - kv.Value < PasswordResetCooldown)
                .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);

            var activeRegs = _registrationRequests
                .Where(kv => now - kv.Value < RegistrationCooldown)
                .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);

            var state = new PersistedState(activeLogins, activeResets, activeRegs);
            File.WriteAllText(StateFile, JsonSerializer.Serialize(state, JsonOpts));
        }
        catch { /* non-critical; in-memory state still works */ }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(StateFile)) return;
            var state = JsonSerializer.Deserialize<PersistedState>(File.ReadAllText(StateFile), JsonOpts);
            if (state is null) return;

            _loginAttempts        = new(state.LoginAttempts,        StringComparer.OrdinalIgnoreCase);
            _passwordResetRequests = new(state.PasswordResetRequests, StringComparer.OrdinalIgnoreCase);
            _registrationRequests  = new(state.RegistrationRequests,  StringComparer.OrdinalIgnoreCase);
        }
        catch { /* corrupt file — start fresh */ }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    public static string FormatRemaining(TimeSpan remaining) =>
        remaining.TotalSeconds < 90
            ? $"{(int)remaining.TotalSeconds} сек."
            : $"{(int)remaining.TotalMinutes} мин.";

    // ── DTOs ─────────────────────────────────────────────────────────────────

    public record LoginEntry(int Count, DateTime? NextAttemptAfter);

    private record PersistedState(
        Dictionary<string, LoginEntry> LoginAttempts,
        Dictionary<string, DateTime>   PasswordResetRequests,
        Dictionary<string, DateTime>   RegistrationRequests);
}
