using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using StudyAssistant.Data;
using StudyAssistant.Models;
using StudyAssistant.Services;

namespace StudyAssistant.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService     _auth;
    private readonly IUserRepository _users;
    private readonly RateLimiter     _rateLimiter;
    private readonly IConfiguration  _config;

    public AuthController(AuthService auth, IUserRepository users,
                          RateLimiter rateLimiter, IConfiguration config)
    {
        _auth        = auth;
        _users       = users;
        _rateLimiter = rateLimiter;
        _config      = config;
    }

    // POST /api/auth/login
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        if (_rateLimiter.IsLoginLocked(req.UsernameOrEmail, out var wait))
            return StatusCode(429, new { error = $"Too many failed attempts. Try again in {RateLimiter.FormatRemaining(wait)}." });

        var error = await _auth.LoginAsync(req.UsernameOrEmail, req.Password);
        if (error != null)
        {
            _rateLimiter.RecordLoginFailure(req.UsernameOrEmail);
            var left = _rateLimiter.RemainingAttemptsBeforeDelay(req.UsernameOrEmail);
            return Unauthorized(new
            {
                error,
                attemptsRemaining = left > 0 ? left : (int?)null
            });
        }

        _rateLimiter.RecordLoginSuccess(req.UsernameOrEmail);

        var user = req.UsernameOrEmail.Contains('@')
            ? await _users.GetByEmailAsync(req.UsernameOrEmail)
            : await _users.GetByUsernameAsync(req.UsernameOrEmail);

        var jwt          = _auth.GenerateJwt(user!);
        var refreshToken = await _auth.GenerateRefreshTokenAsync(user!);

        return Ok(new { token = jwt, refreshToken, user = Summary(user!) });
    }

    // POST /api/auth/register
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        if (_rateLimiter.IsRegistrationThrottled(req.Email, out var wait))
            return StatusCode(429, new { error = $"Registration attempted recently. Try again in {RateLimiter.FormatRemaining(wait)}." });

        UserRole role;
        if (string.Equals(req.Role, "teacher", StringComparison.OrdinalIgnoreCase))
        {
            var expectedCode = _config["TeacherRegistrationCode"] ?? "";
            if (string.IsNullOrEmpty(req.TeacherRegistrationCode) ||
                !CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(req.TeacherRegistrationCode),
                    Encoding.UTF8.GetBytes(expectedCode)))
                return Unauthorized(new { error = "Invalid teacher registration code." });
            role = UserRole.Teacher;
        }
        else
        {
            role = UserRole.Student;
        }

        var (user, errors) = await _auth.RegisterAsync(
            req.Username, req.Email, req.Password, role,
            req.FullName, req.Grade, req.ClassLetter, req.School);

        if (user == null)
            return BadRequest(new { errors });

        _rateLimiter.RecordRegistration(req.Email);

        var jwt          = _auth.GenerateJwt(user);
        var refreshToken = await _auth.GenerateRefreshTokenAsync(user);

        return Ok(new { token = jwt, refreshToken, user = Summary(user) });
    }

    // POST /api/auth/refresh
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest req)
    {
        var (jwt, newRefreshToken, error) = await _auth.ExchangeRefreshTokenAsync(req.RefreshToken);
        if (error != null)
            return Unauthorized(new { error });

        return Ok(new { token = jwt, refreshToken = newRefreshToken });
    }

    // POST /api/auth/logout
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest req)
    {
        await _auth.RevokeRefreshTokenAsync(req.RefreshToken);
        return Ok(new { message = "Logged out successfully." });
    }

    // POST /api/auth/forgot-password
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest req)
    {
        if (_rateLimiter.IsPasswordResetThrottled(req.Email, out var wait))
            return StatusCode(429, new { error = $"Reset already requested. Try again in {RateLimiter.FormatRemaining(wait)}." });

        var (token, _) = await _auth.RequestPasswordResetAsync(req.Email);
        _rateLimiter.RecordPasswordResetRequest(req.Email);

        // Always 200 regardless of whether the email exists — avoids account enumeration.
        // TODO: email the token instead of returning it once email sending is wired up.
        return Ok(new
        {
            message = "If that email is registered, a reset token has been generated.",
            token   // remove this field and send via email in production
        });
    }

    // POST /api/auth/reset-password
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req)
    {
        var (success, errors) = await _auth.ResetPasswordAsync(req.Email, req.Token, req.NewPassword);
        if (!success)
            return BadRequest(new { errors });
        return Ok(new { message = "Password reset successfully." });
    }

    private static object Summary(ApplicationUser u) => new
    {
        id       = u.Id,
        username = u.UserName,
        email    = u.Email,
        fullName = u.FullName,
        role     = u.Role.ToString().ToLower(),
        grade    = u.Grade
    };
}

public record LoginRequest(
    [Required, MaxLength(254)] string UsernameOrEmail,
    [Required, MaxLength(128)] string Password);

public record RegisterRequest(
    [Required, MaxLength(50)]  string  Username,
    [Required, MaxLength(254)] string  Email,
    [Required, MaxLength(128)] string  Password,
    [Required, MaxLength(150)] string  FullName,
    [Required, MaxLength(20)]  string  Role,
    int?    Grade,
    [MaxLength(5)]   string? ClassLetter,
    [MaxLength(200)] string? School,
    [MaxLength(100)] string? TeacherRegistrationCode);

public record RefreshRequest([Required, MaxLength(128)] string RefreshToken);

public record ForgotPasswordRequest([Required, MaxLength(254)] string Email);

public record ResetPasswordRequest(
    [Required, MaxLength(254)]  string Email,
    [Required, MaxLength(2048)] string Token,
    [Required, MaxLength(128)]  string NewPassword);
