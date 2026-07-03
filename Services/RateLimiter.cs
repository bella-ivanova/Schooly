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

    private const string LoginType            = "login";
    private const string IpLoginType          = "ip_login";
    private const string PasswordResetType    = "password_reset";
    private const string ResetCodeAttemptType = "reset_code_attempt";
    private const string RegistrationType     = "registration";
    private const string GeneralApiType       = "general_api";

    private const int ResetCodeMaxAttempts = 5;

    // 20 requests per minute per IP on token-exchange and logout endpoints.
    private static readonly TimeSpan GeneralApiWindow = TimeSpan.FromMinutes(1);
    private const int GeneralApiMaxRequests = 20;

    // ── Login ────────────────────────────────────────────────────────────────

    public bool IsLoginLocked(string usernameOrEmail, string? ip, out TimeSpan remaining)
    {
        remaining = TimeSpan.Zero;
        using var db = dbFactory.CreateDbContext();

        // Account-based check
        var key   = usernameOrEmail.ToLowerInvariant();
        var entry = db.RateLimitEntries.FirstOrDefault(e => e.Key == key && e.Type == LoginType);
        if (entry?.NextAttemptAfter != null)
        {
            if (DateTime.UtcNow < entry.NextAttemptAfter)
            {
                remaining = entry.NextAttemptAfter.Value - DateTime.UtcNow;
                return true;
            }
            db.RateLimitEntries
                .Where(e => e.Key == key && e.Type == LoginType
                         && e.NextAttemptAfter != null && e.NextAttemptAfter <= DateTime.UtcNow)
                .ExecuteUpdate(s => s.SetProperty(e => e.NextAttemptAfter, (DateTime?)null));
        }

        // IP-based check — secondary, loose limit (3 free attempts, caps at 30 s)
        if (ip is not null)
        {
            var ipKey   = "ip:" + ip;
            var ipEntry = db.RateLimitEntries.FirstOrDefault(e => e.Key == ipKey && e.Type == IpLoginType);
            if (ipEntry?.NextAttemptAfter != null)
            {
                if (DateTime.UtcNow < ipEntry.NextAttemptAfter)
                {
                    remaining = ipEntry.NextAttemptAfter.Value - DateTime.UtcNow;
                    return true;
                }
                db.RateLimitEntries
                    .Where(e => e.Key == ipKey && e.Type == IpLoginType
                             && e.NextAttemptAfter != null && e.NextAttemptAfter <= DateTime.UtcNow)
                    .ExecuteUpdate(s => s.SetProperty(e => e.NextAttemptAfter, (DateTime?)null));
            }
        }

        return false;
    }

    // Atomically increments the failure count and sets the progressive delay in one round-trip.
    // Returns the new failure count so callers can compute attemptsBeforeDelay without a second query.
    public int RecordLoginFailure(string usernameOrEmail, string? ip)
    {
        var key = usernameOrEmail.ToLowerInvariant();
        using var db = dbFactory.CreateDbContext();

        var count = db.Database.SqlQuery<int>($"""
            INSERT INTO rate_limit_entries (key, type, failure_count, next_attempt_after)
            VALUES ({key}, {LoginType}, 1, NOW() + INTERVAL '3 seconds')
            ON CONFLICT (key, type) DO UPDATE
              SET failure_count      = rate_limit_entries.failure_count + 1,
                  next_attempt_after = NOW() + (
                    CASE LEAST(rate_limit_entries.failure_count + 1, 4)
                      WHEN 1 THEN INTERVAL '3 seconds'
                      WHEN 2 THEN INTERVAL '10 seconds'
                      WHEN 3 THEN INTERVAL '30 seconds'
                      ELSE         INTERVAL '60 seconds'
                    END
                  )
            RETURNING failure_count
            """).First();

        // Secondary IP-based counter: 3 free attempts, then 3 s / 10 s / 30 s (capped).
        if (ip is not null)
        {
            var ipKey = "ip:" + ip;
            db.Database.ExecuteSql($"""
                INSERT INTO rate_limit_entries (key, type, failure_count, next_attempt_after)
                VALUES ({ipKey}, {IpLoginType}, 1, NULL)
                ON CONFLICT (key, type) DO UPDATE
                  SET failure_count      = rate_limit_entries.failure_count + 1,
                      next_attempt_after = CASE
                        WHEN rate_limit_entries.failure_count + 1 < 3  THEN NULL
                        WHEN rate_limit_entries.failure_count + 1 = 3  THEN NOW() + INTERVAL '3 seconds'
                        WHEN rate_limit_entries.failure_count + 1 = 4  THEN NOW() + INTERVAL '10 seconds'
                        ELSE                                                 NOW() + INTERVAL '30 seconds'
                      END
                """);
        }

        return count;
    }

    public void RecordLoginSuccess(string usernameOrEmail)
    {
        var key = usernameOrEmail.ToLowerInvariant();
        using var db = dbFactory.CreateDbContext();
        db.RateLimitEntries
            .Where(e => e.Key == key && e.Type == LoginType)
            .ExecuteDelete();
    }

    public static int AttemptsBeforeDelay(int failureCount) =>
        Math.Max(0, LoginDelays.Length - 1 - failureCount);

    // ── Password reset ───────────────────────────────────────────────────────

    // Atomically claims a password-reset slot if the cooldown has expired.
    // Must be called before the async reset work; returns throttled=true when the slot is still held.
    public (bool throttled, TimeSpan remaining) TryRecordPasswordResetRequest(string email)
    {
        var key = email.ToLowerInvariant();
        using var db = dbFactory.CreateDbContext();

        // ON CONFLICT DO UPDATE WHERE only fires when the cooldown has passed; returns 0 rows when throttled.
        var claimed = db.Database.SqlQuery<int>($"""
            INSERT INTO rate_limit_entries (key, type, last_request_at, failure_count)
            VALUES ({key}, {PasswordResetType}, NOW(), 0)
            ON CONFLICT (key, type) DO UPDATE
              SET last_request_at = EXCLUDED.last_request_at
              WHERE rate_limit_entries.last_request_at IS NULL
                 OR rate_limit_entries.last_request_at < NOW() - {PasswordResetCooldown}
            RETURNING 1
            """).FirstOrDefault();

        if (claimed != 0) return (false, TimeSpan.Zero);

        // Slot not claimed — read the held timestamp to compute the wait for the caller.
        var lastAt = db.RateLimitEntries
            .Where(e => e.Key == key && e.Type == PasswordResetType)
            .Select(e => e.LastRequestAt)
            .FirstOrDefault();

        if (lastAt is null) return (false, TimeSpan.Zero);
        var remaining = PasswordResetCooldown - (DateTime.UtcNow - lastAt.Value);
        return (true, remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero);
    }

    // ── Reset code verification ──────────────────────────────────────────────

    // Atomically increments the attempt counter for reset code verification.
    // Returns throttled=true after ResetCodeMaxAttempts failures.
    // Call ClearResetCodeAttempts when a new code is issued to reset the counter.
    public (bool throttled, int attemptsUsed) TryRecordResetCodeAttempt(string email)
    {
        var key = email.ToLowerInvariant();
        using var db = dbFactory.CreateDbContext();

        var count = db.Database.SqlQuery<int>($"""
            INSERT INTO rate_limit_entries (key, type, failure_count, last_request_at)
            VALUES ({key}, {ResetCodeAttemptType}, 1, NOW())
            ON CONFLICT (key, type) DO UPDATE
              SET failure_count   = rate_limit_entries.failure_count + 1,
                  last_request_at = NOW()
            RETURNING failure_count
            """).First();

        return (count > ResetCodeMaxAttempts, count);
    }

    public void ClearResetCodeAttempts(string email)
    {
        var key = email.ToLowerInvariant();
        using var db = dbFactory.CreateDbContext();
        db.RateLimitEntries
            .Where(e => e.Key == key && e.Type == ResetCodeAttemptType)
            .ExecuteDelete();
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
        db.Database.ExecuteSql($"""
            INSERT INTO rate_limit_entries (key, type, last_request_at, failure_count)
            VALUES ({key}, {RegistrationType}, NOW(), 0)
            ON CONFLICT (key, type) DO UPDATE
              SET last_request_at = EXCLUDED.last_request_at
            """);
    }

    // ── General API (refresh / logout) ──────────────────────────────────────

    // Returns true when the IP has exceeded GeneralApiMaxRequests within GeneralApiWindow.
    // Uses a sliding-window count stored in the rate_limit_entries table.
    public bool IsGeneralApiThrottled(string? ip, out TimeSpan remaining)
    {
        remaining = TimeSpan.Zero;
        if (ip is null) return false;

        var key = "ip:" + ip;
        using var db = dbFactory.CreateDbContext();

        var claimed = db.Database.SqlQuery<int>($"""
            INSERT INTO rate_limit_entries (key, type, failure_count, last_request_at)
            VALUES ({key}, {GeneralApiType}, 1, NOW())
            ON CONFLICT (key, type) DO UPDATE
              SET failure_count    = CASE
                                       WHEN rate_limit_entries.last_request_at < NOW() - {GeneralApiWindow}
                                       THEN 1
                                       ELSE rate_limit_entries.failure_count + 1
                                     END,
                  last_request_at  = NOW()
            RETURNING failure_count
            """).First();

        if (claimed <= GeneralApiMaxRequests) return false;

        remaining = GeneralApiWindow;
        return true;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    public static string FormatRemaining(TimeSpan remaining) =>
        remaining.TotalSeconds < 90
            ? $"{(int)remaining.TotalSeconds} сек."
            : $"{(int)remaining.TotalMinutes} мин.";
}
