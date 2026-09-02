using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using StudyAssistant.Data;
using StudyAssistant.Models;

namespace StudyAssistant.Services;

public record AuthUserSummaryDto(string Id, string? Username, string? Email, string FullName,
    string Role, int? Grade, string? SchoolName, string? ClassLetter);

public class AuthService
{
    private readonly IUserRepository _users;
    private readonly IConfiguration  _config;
    private readonly AppDbContext    _db;
    private readonly IEmailService   _email;
    private readonly ILogger<AuthService> _logger;

    public AuthService(IUserRepository users, IConfiguration config, AppDbContext db,
                       IEmailService email, ILogger<AuthService> logger)
    {
        _users  = users;
        _config = config;
        _db     = db;
        _email  = email;
        _logger = logger;
    }

    // Registers a new user. Returns the created user on success.
    public async Task<(ApplicationUser? User, IReadOnlyList<string> Errors)> RegisterAsync(
        string username, string email, string password, UserRole role,
        string fullName = "", int? grade = null, string? classLetter = null,
        int? schoolId = null, int? classId = null)
    {
        if (role == UserRole.Student && (grade is null or < 1 or > 12))
            return (null, new[] { "Students must have a grade between 1 and 12." });

        var normLetter = role == UserRole.Student && !string.IsNullOrWhiteSpace(classLetter)
            ? classLetter.Trim().ToUpperInvariant()
            : null;

        var user = new ApplicationUser
        {
            UserName    = username,
            Email       = email,
            FullName    = fullName,
            Role        = role,
            Grade       = role == UserRole.Student ? grade : null,
            ClassLetter = normLetter,
            SchoolId    = schoolId,
            CreatedAt   = DateTime.UtcNow
        };

        var (success, errors) = await _users.CreateAsync(user, password);
        if (!success)
            return (null, errors);

        if (classId != null)
        {
            _db.ClassStudents.Add(new ClassStudent { ClassId = classId.Value, StudentId = user.Id });
            await _db.SaveChangesAsync();
        }

        return (user, Array.Empty<string>());
    }

    // Resolves an optional class-join code into the class's Id and its school's Id.
    // An empty code is not an error (registration/joining without a class is always
    // allowed) — a non-empty code that matches nothing IS an error, so the caller can
    // surface clear feedback instead of silently registering/joining without a class.
    public async Task<(bool Ok, int? ResolvedClassId, int? ResolvedSchoolId)> ResolveClassJoinCodeAsync(string? suppliedCode)
    {
        if (string.IsNullOrEmpty(suppliedCode))
            return (true, null, null);

        var activeCodes = await _db.ClassJoinCodes
            .Where(c => c.IsActive)
            .Select(c => new { c.ClassId, c.Code })
            .ToListAsync();

        var suppliedBytes = Encoding.UTF8.GetBytes(suppliedCode);
        foreach (var candidate in activeCodes)
        {
            var candidateBytes = Encoding.UTF8.GetBytes(candidate.Code);
            if (candidateBytes.Length == suppliedBytes.Length &&
                CryptographicOperations.FixedTimeEquals(suppliedBytes, candidateBytes))
            {
                var schoolId = await _db.Classes
                    .Where(c => c.Id == candidate.ClassId)
                    .Select(c => (int?)c.SchoolId)
                    .FirstOrDefaultAsync();
                return (true, candidate.ClassId, schoolId);
            }
        }

        return (false, null, null);
    }

    // Attaches an already-registered Student to an additional class via that class's
    // active join code. A student may belong to multiple classes, but they must all
    // belong to the same school: the first join sets SchoolId, later joins are
    // rejected if the code's class belongs to a different school.
    public async Task<(bool Success, string? Error, int? ClassId, int? SchoolId)> JoinClassAsync(string userId, string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return (false, "A class code is required.", null, null);

        var user = await _users.GetByIdAsync(userId);
        if (user == null)
            return (false, "User not found.", null, null);
        if (user.Role != UserRole.Student)
            return (false, "Only students can join a class this way.", null, null);

        var (matched, classId, schoolId) = await ResolveClassJoinCodeAsync(code);
        if (!matched || classId == null)
            return (false, "Invalid class code.", null, null);

        if (user.SchoolId != null && user.SchoolId != schoolId)
            return (false, "This class belongs to a different school than the one you're already attached to.", null, null);

        var alreadyMember = await _db.ClassStudents
            .AnyAsync(cs => cs.ClassId == classId && cs.StudentId == userId);
        if (alreadyMember)
            return (false, "You're already in this class.", null, null);

        _db.ClassStudents.Add(new ClassStudent { ClassId = classId.Value, StudentId = userId });
        if (user.SchoolId == null)
        {
            user.SchoolId = schoolId;
            await _users.UpdateAsync(user);
        }
        await _db.SaveChangesAsync();

        return (true, null, classId, schoolId);
    }

    // Read-only lookup backing the student-facing "what classes am I in" endpoint.
    public async Task<(int? SchoolId, string? SchoolName, IReadOnlyList<(int ClassId, string ClassName)> Classes)>
        GetStudentClassesAsync(string userId)
    {
        var user = await _users.GetByIdAsync(userId);
        if (user == null)
            return (null, null, Array.Empty<(int, string)>());

        string? schoolName = user.SchoolId != null
            ? await _db.Schools.Where(s => s.Id == user.SchoolId).Select(s => s.Name).FirstOrDefaultAsync()
            : null;

        var classes = await _db.ClassStudents
            .Where(cs => cs.StudentId == userId)
            .Include(cs => cs.Class)
            .Select(cs => new { cs.ClassId, Name = cs.Class!.Name })
            .ToListAsync();

        return (user.SchoolId, schoolName, classes.Select(c => (c.ClassId, c.Name)).ToList());
    }

    // Resolves a teacher registration attempt against the target school's active registration code.
    // Returns (true, SchoolId) on a match, or (false, null) if the code is empty/invalid.
    public async Task<(bool Ok, int? ResolvedSchoolId)> ResolveTeacherRegistrationAsync(string suppliedCode)
    {
        if (string.IsNullOrEmpty(suppliedCode))
            return (false, null);

        var activeCodes = await _db.SchoolTeacherCodes
            .Where(c => c.IsActive)
            .Select(c => new { c.SchoolId, c.Code })
            .ToListAsync();

        var suppliedBytes = Encoding.UTF8.GetBytes(suppliedCode);
        foreach (var candidate in activeCodes)
        {
            var candidateBytes = Encoding.UTF8.GetBytes(candidate.Code);
            if (candidateBytes.Length == suppliedBytes.Length &&
                CryptographicOperations.FixedTimeEquals(suppliedBytes, candidateBytes))
                return (true, candidate.SchoolId);
        }

        return (false, null);
    }

    // Validates credentials. Returns null on success, or an error message on failure.
    public async Task<string?> LoginAsync(string usernameOrEmail, string password)
    {
        var user = usernameOrEmail.Contains('@')
            ? await _users.GetByEmailAsync(usernameOrEmail)
            : await _users.GetByUsernameAsync(usernameOrEmail);

        if (user == null)
            return "Invalid username or password.";

        if (!await _users.CheckPasswordAsync(user, password))
            return "Invalid username or password.";

        return null;
    }

    // Generates a 6-digit reset code, stores it in the DB, and emails it to the user.
    // Returns (false, error) if the email is not registered.
    public async Task<(bool Found, string? Error)> RequestPasswordResetAsync(string email)
    {
        var user = await _users.GetByEmailAsync(email);
        if (user == null)
            return (false, "Email not found.");

        // Invalidate any outstanding codes for this user.
        await _db.PasswordResetCodes
            .Where(c => c.UserId == user.Id && !c.IsUsed)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsUsed, true));

        var code = System.Security.Cryptography.RandomNumberGenerator
            .GetInt32(1_000_000).ToString("D6");

        _db.PasswordResetCodes.Add(new Models.PasswordResetCode
        {
            UserId    = user.Id,
            Code      = code,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        try
        {
            await _email.SendPasswordResetCodeAsync(user.Email!, code);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password reset email to {Email}", email);
        }

        return (true, null);
    }

    // Checks whether the supplied code is valid without consuming it.
    public async Task<bool> VerifyResetCodeAsync(string email, string code)
    {
        var user = await _users.GetByEmailAsync(email);
        if (user == null) return false;

        return await _db.PasswordResetCodes
            .AnyAsync(c => c.UserId == user.Id
                        && c.Code == code
                        && !c.IsUsed
                        && c.ExpiresAt > DateTime.UtcNow);
    }

    // Validates the code, checks the new password differs, then changes the password.
    public async Task<(bool Success, IReadOnlyList<string> Errors)> ResetPasswordAsync(
        string email, string code, string newPassword)
    {
        var user = await _users.GetByEmailAsync(email);
        if (user == null)
            return (false, new[] { "Invalid or expired reset code." });

        if (await _users.CheckPasswordAsync(user, newPassword))
            return (false, new[] { "New password must be different from your current password." });

        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);

        var resetCode = await _db.PasswordResetCodes
            .SingleOrDefaultAsync(c => c.UserId == user.Id
                                    && c.Code == code
                                    && !c.IsUsed
                                    && c.ExpiresAt > DateTime.UtcNow);

        if (resetCode == null)
        {
            await tx.RollbackAsync();
            return (false, new[] { "Invalid or expired reset code." });
        }

        resetCode.IsUsed = true;
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return await _users.ChangePasswordDirectlyAsync(user, newPassword);
    }

    // Updates the caller's own display name and, for students, their grade.
    // Email, school, and class membership are not editable through this path.
    public async Task<(ApplicationUser? User, string? Error)> UpdateProfileAsync(string userId, string fullName, int? grade)
    {
        var user = await _users.GetByIdAsync(userId);
        if (user == null)
            return (null, "User not found.");

        var trimmedName = fullName.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
            return (null, "Full name is required.");

        user.FullName = trimmedName;

        if (user.Role == UserRole.Student)
        {
            if (grade is null or < 1 or > 12)
                return (null, "Students must have a grade between 1 and 12.");
            user.Grade = grade;
        }

        if (!await _users.UpdateAsync(user))
            return (null, "Failed to update profile.");

        return (user, null);
    }

    // Builds the caller-facing summary DTO for login/register/update-profile responses.
    // SchoolName is resolved with a direct AppDbContext query rather than a School nav-prop
    // read, because UserManager.FindByIdAsync (behind IUserRepository) does not eager-load
    // ApplicationUser.School — see GetStudentClassesAsync for the identical pattern.
    public async Task<AuthUserSummaryDto> BuildUserSummaryAsync(ApplicationUser user)
    {
        string? schoolName = user.SchoolId != null
            ? await _db.Schools.Where(s => s.Id == user.SchoolId).Select(s => s.Name).FirstOrDefaultAsync()
            : null;

        return new AuthUserSummaryDto(
            user.Id, user.UserName, user.Email, user.FullName,
            user.Role.ToString().ToLower(), user.Grade, schoolName,
            user.Role == UserRole.Student ? user.ClassLetter : null);
    }

    // Verifies the current password (unlike the post-reset-code ChangePasswordDirectlyAsync)
    // before applying a new one.
    public async Task<(bool Success, IReadOnlyList<string> Errors)> ChangePasswordAsync(
        string userId, string currentPassword, string newPassword)
    {
        var user = await _users.GetByIdAsync(userId);
        if (user == null)
            return (false, new[] { "User not found." });

        if (currentPassword == newPassword)
            return (false, new[] { "New password must be different from your current password." });

        return await _users.ChangePasswordAsync(user, currentPassword, newPassword);
    }

    public string GenerateJwt(ApplicationUser user)
    {
        var secret   = _config["Jwt:Secret"]   ?? throw new InvalidOperationException("Jwt:Secret is not configured.");
        var issuer   = _config["Jwt:Issuer"]   ?? "StudyAssistant";
        var audience = _config["Jwt:Audience"] ?? "StudyAssistantUsers";
        var expMinutes = int.TryParse(_config["Jwt:ExpiryMinutes"], out var m) ? m : 60;

        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,        user.Id),
            new Claim(JwtRegisteredClaimNames.Email,      user.Email ?? ""),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName ?? ""),
            new Claim("role",  user.Role.ToString()),
            new Claim("grade", user.Grade?.ToString() ?? ""),
            new Claim(JwtRegisteredClaimNames.Jti,        Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer:             issuer,
            audience:           audience,
            claims:             claims,
            expires:            DateTime.UtcNow.AddMinutes(expMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<string> GenerateRefreshTokenAsync(ApplicationUser user)
    {
        var expiryDays = int.TryParse(_config["Jwt:RefreshTokenExpiryDays"], out var d) ? d : 7;

        var tokenBytes = RandomNumberGenerator.GetBytes(64);
        var tokenValue = Convert.ToHexString(tokenBytes).ToLowerInvariant();

        _db.RefreshTokens.Add(new RefreshToken
        {
            Token     = tokenValue,
            UserId    = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(expiryDays),
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        return tokenValue;
    }

    public async Task<(string? Jwt, string? NewRefreshToken, string? Error)> ExchangeRefreshTokenAsync(string token)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);

        var stored = await _db.RefreshTokens
            .Include(r => r.User)
            .SingleOrDefaultAsync(r => r.Token == token);

        if (stored == null || stored.IsRevoked || stored.ExpiresAt <= DateTime.UtcNow)
        {
            await tx.RollbackAsync();
            return (null, null, "Invalid or expired refresh token.");
        }

        stored.IsRevoked = true;

        try
        {
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch (DbUpdateException)
        {
            return (null, null, "Invalid or expired refresh token.");
        }

        var newRefreshToken = await GenerateRefreshTokenAsync(stored.User);
        var newJwt          = GenerateJwt(stored.User);

        return (newJwt, newRefreshToken, null);
    }

    public async Task<bool> RevokeRefreshTokenAsync(string token)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);

        var stored = await _db.RefreshTokens.SingleOrDefaultAsync(r => r.Token == token);
        if (stored == null || stored.IsRevoked)
        {
            await tx.RollbackAsync();
            return false;
        }

        stored.IsRevoked = true;
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return true;
    }
}
