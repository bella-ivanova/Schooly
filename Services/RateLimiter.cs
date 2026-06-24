using Microsoft.EntityFrameworkCore;
using StudyAssistant.Data;
using StudyAssistant.Models;

namespace StudyAssistant.Services;

public class RateLimiter(IDbContextFactory<AppDbContext> dbFactory)
{
    // Progressive delays per consecutive failure (index = failure count, capped at last entry).
    // Replaces the hard 15-min lockout so an attacker cannot permanently deny access to any account.
    private static readonly TimeSpan[] LoginDelays =
    [
        TimeSpan.Zero,            // 0 failures: no wait
        TimeSpan.FromSeconds(3),  // 1st failure
        TimeSpan.FromSeconds(10), // 2nd
        TimeSpan.FromSeconds(30), // 3rd
        TimeSpan.FromSeconds(60), // 4th and beyond
    ];
    private static readonly TimeSpan PasswordResetCooldown = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RegistrationCooldown  = TimeSpan.FromMinutes(2);

    private const string LoginType         = "login";
    private const string PasswordResetType = "password_reset";
    private const string RegistrationType  = "registration";

    // ── Login ────────────────────────────────────────────────────────────────

    public bool IsLoginLocked(string usernameOrEmail, out TimeSpan remaining)
    {
        remaining = TimeSpan.Zero;
        var key = usernameOrEmail.ToLowerInvariant();
        using var db = dbFactory.CreateDbContext();
        var entry = db.RateLimitEntries.FirstOrDefault(e => e.Key == key && e.Type == LoginType);

        if (entry is null || entry.NextAttemptAfter is null) return false;

        if (DateTime.UtcNow < entry.NextAttemptAfter)
        {
            remaining = entry.NextAttemptAfter.Value - DateTime.UtcNow;
            return true;
        }

        // Delay expired — clear it but keep the failure count so delays keep growing
        entry.NextAttemptAfter = null;
        db.SaveChanges();
        return false;
    }

    public void RecordLoginFailure(string usernameOrEmail)
    {
        var key = usernameOrEmail.ToLowerInvariant();
        using var db = dbFactory.CreateDbContext();
        var entry = db.RateLimitEntries.FirstOrDefault(e => e.Key == key && e.Type == LoginType);

        int newCount;
        DateTime? nextAllowed;

        if (entry is null)
        {
            newCount = 1;
            var delay = LoginDelays[Math.Min(newCount, LoginDelays.Length - 1)];
            nextAllowed = delay > TimeSpan.Zero ? DateTime.UtcNow.Add(delay) : null;
            db.RateLimitEntries.Add(new RateLimitEntry
            {
                Key = key, Type = LoginType,
                FailureCount = newCount, NextAttemptAfter = nextAllowed
            });
        }
        else
        {
            newCount = entry.FailureCount + 1;
            var delay = LoginDelays[Math.Min(newCount, LoginDelays.Length - 1)];
            nextAllowed = delay > TimeSpan.Zero ? DateTime.UtcNow.Add(delay) : null;
            entry.FailureCount = newCount;
            entry.NextAttemptAfter = nextAllowed;
        }

        db.SaveChanges();
    }

    public void RecordLoginSuccess(string usernameOrEmail)
    {
        var key = usernameOrEmail.ToLowerInvariant();
        using var db = dbFactory.CreateDbContext();
        var entry = db.RateLimitEntries.FirstOrDefault(e => e.Key == key && e.Type == LoginType);
        if (entry is not null)
        {
            db.RateLimitEntries.Remove(entry);
            db.SaveChanges();
        }
    }

    public int RemainingAttemptsBeforeDelay(string usernameOrEmail)
    {
        var key = usernameOrEmail.ToLowerInvariant();
        using var db = dbFactory.CreateDbContext();
        var entry = db.RateLimitEntries.FirstOrDefault(e => e.Key == key && e.Type == LoginType);
        return Math.Max(0, LoginDelays.Length - 1 - (entry?.FailureCount ?? 0));
    }

    // ── Password reset ───────────────────────────────────────────────────────

    public bool IsPasswordResetThrottled(string email, out TimeSpan remaining)
    {
        remaining = TimeSpan.Zero;
        var key = email.ToLowerInvariant();
        using var db = dbFactory.CreateDbContext();
        var entry = db.RateLimitEntries.FirstOrDefault(e => e.Key == key && e.Type == PasswordResetType);

        if (entry?.LastRequestAt is null) return false;
        var elapsed = DateTime.UtcNow - entry.LastRequestAt.Value;
        if (elapsed < PasswordResetCooldown)
        {
            remaining = PasswordResetCooldown - elapsed;
            return true;
        }
        return false;
    }

    public void RecordPasswordResetRequest(string email)
    {
        var key = email.ToLowerInvariant();
        using var db = dbFactory.CreateDbContext();
        var entry = db.RateLimitEntries.FirstOrDefault(e => e.Key == key && e.Type == PasswordResetType);

        if (entry is null)
            db.RateLimitEntries.Add(new RateLimitEntry { Key = key, Type = PasswordResetType, LastRequestAt = DateTime.UtcNow });
        else
            entry.LastRequestAt = DateTime.UtcNow;

        db.SaveChanges();
    }

    // ── Registration ─────────────────────────────────────────────────────────

    public bool IsRegistrationThrottled(string email, out TimeSpan remaining)
    {
        remaining = TimeSpan.Zero;
        var key = email.ToLowerInvariant();
        using var db = dbFactory.CreateDbContext();
        var entry = db.RateLimitEntries.FirstOrDefault(e => e.Key == key && e.Type == RegistrationType);

        if (entry?.LastRequestAt is null) return false;
        var elapsed = DateTime.UtcNow - entry.LastRequestAt.Value;
        if (elapsed < RegistrationCooldown)
        {
            remaining = RegistrationCooldown - elapsed;
            return true;
        }
        return false;
    }

    public void RecordRegistration(string email)
    {
        var key = email.ToLowerInvariant();
        using var db = dbFactory.CreateDbContext();
        var entry = db.RateLimitEntries.FirstOrDefault(e => e.Key == key && e.Type == RegistrationType);

        if (entry is null)
            db.RateLimitEntries.Add(new RateLimitEntry { Key = key, Type = RegistrationType, LastRequestAt = DateTime.UtcNow });
        else
            entry.LastRequestAt = DateTime.UtcNow;

        db.SaveChanges();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    public static string FormatRemaining(TimeSpan remaining) =>
        remaining.TotalSeconds < 90
            ? $"{(int)remaining.TotalSeconds} сек."
            : $"{(int)remaining.TotalMinutes} мин.";
}
