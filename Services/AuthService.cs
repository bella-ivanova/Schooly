using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using StudyAssistant.Data;
using StudyAssistant.Models;

namespace StudyAssistant.Services;

public class AuthService
{
    private readonly IUserRepository _users;
    private readonly IConfiguration _config;

    public AuthService(IUserRepository users, IConfiguration config)
    {
        _users  = users;
        _config = config;
    }

    // Registers a new user. Returns the created user on success.
    public async Task<(ApplicationUser? User, IReadOnlyList<string> Errors)> RegisterAsync(
        string username, string email, string password, UserRole role,
        string fullName = "", int? grade = null, string? classLetter = null, string? school = null)
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
            School      = school,
            CreatedAt   = DateTime.UtcNow
        };

        var (success, errors) = await _users.CreateAsync(user, password);
        return success ? (user, Array.Empty<string>()) : (null, errors);
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

    // Generates a password reset token (send this to the user via email/console).
    public async Task<(string? Token, string? Error)> RequestPasswordResetAsync(string email)
    {
        var user = await _users.GetByEmailAsync(email);
        if (user == null)
            return (null, "No account found with that email.");

        var token = await _users.GeneratePasswordResetTokenAsync(user);
        return (token, null);
    }

    // Applies a previously issued reset token.
    public async Task<(bool Success, IReadOnlyList<string> Errors)> ResetPasswordAsync(
        string email, string token, string newPassword)
    {
        var user = await _users.GetByEmailAsync(email);
        if (user == null)
            return (false, new[] { "No account found with that email." });

        return await _users.ResetPasswordAsync(user, token, newPassword);
    }

    // Validates a JWT and returns its claims. Returns null if invalid or expired.
    public ClaimsPrincipal? ValidateToken(string token)
    {
        var secret   = _config["Jwt:Secret"]   ?? throw new InvalidOperationException("Jwt:Secret is not configured.");
        var issuer   = _config["Jwt:Issuer"]   ?? "StudyAssistant";
        var audience = _config["Jwt:Audience"] ?? "StudyAssistantUsers";

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var handler = new JwtSecurityTokenHandler();

        try
        {
            return handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey         = key,
                ValidateIssuer           = true,
                ValidIssuer              = issuer,
                ValidateAudience         = true,
                ValidAudience            = audience,
                ValidateLifetime         = true,
                ClockSkew                = TimeSpan.Zero
            }, out _);
        }
        catch { return null; }
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    public string GenerateJwt(ApplicationUser user)
    {
        var secret  = _config["Jwt:Secret"]   ?? throw new InvalidOperationException("Jwt:Secret is not configured.");
        var issuer  = _config["Jwt:Issuer"]   ?? "StudyAssistant";
        var audience = _config["Jwt:Audience"] ?? "StudyAssistantUsers";
        var expHours = int.TryParse(_config["Jwt:ExpiryHours"], out var h) ? h : 24;

        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,   user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? ""),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName ?? ""),
            new Claim("role",  user.Role.ToString()),
            new Claim("grade", user.Grade?.ToString() ?? ""),
            new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer:    issuer,
            audience:  audience,
            claims:    claims,
            expires:   DateTime.UtcNow.AddHours(expHours),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}