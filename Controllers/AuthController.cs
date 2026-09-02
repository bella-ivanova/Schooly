using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
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
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        if (_rateLimiter.IsLoginLocked(req.UsernameOrEmail, ip, out _))
            return StatusCode(429, new { error = "Too many failed login attempts. Please wait before trying again." });

        var error = await _auth.LoginAsync(req.UsernameOrEmail, req.Password);
        if (error != null)
        {
            var newCount = _rateLimiter.RecordLoginFailure(req.UsernameOrEmail, ip);
            var left = RateLimiter.AttemptsBeforeDelay(newCount);
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

        return Ok(new { token = jwt, refreshToken, user = await _auth.BuildUserSummaryAsync(user!) });
    }

    // POST /api/auth/register
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        if (_rateLimiter.IsRegistrationThrottled(req.Email, out _))
            return StatusCode(429, new { error = "Registration attempted too recently. Please wait before trying again." });

        UserRole role;
        int? resolvedSchoolId = null;
        int? resolvedClassId = null;

        if (string.Equals(req.Role, "teacher", StringComparison.OrdinalIgnoreCase))
        {
            var (matched, schoolIdFromCode) = await _auth.ResolveTeacherRegistrationAsync(req.TeacherRegistrationCode ?? "");
            if (!matched)
                return Unauthorized(new { error = "Invalid school registration code." });

            role = UserRole.Teacher;
            resolvedSchoolId = schoolIdFromCode;
        }
        else
        {
            role = UserRole.Student;

            var (codeOk, classIdFromCode, schoolIdFromCode) = await _auth.ResolveClassJoinCodeAsync(req.ClassJoinCode);
            if (!codeOk)
                return BadRequest(new { error = "Invalid class code." });

            resolvedClassId  = classIdFromCode;
            resolvedSchoolId = schoolIdFromCode;
        }

        var (user, errors) = await _auth.RegisterAsync(
            req.Username, req.Email, req.Password, role,
            req.FullName, req.Grade, req.ClassLetter, resolvedSchoolId, resolvedClassId);

        if (user == null)
            return BadRequest(new { errors });

        _rateLimiter.RecordRegistration(req.Email);

        var jwt          = _auth.GenerateJwt(user);
        var refreshToken = await _auth.GenerateRefreshTokenAsync(user);

        return Ok(new { token = jwt, refreshToken, user = await _auth.BuildUserSummaryAsync(user) });
    }

    // POST /api/auth/refresh
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest req)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        if (_rateLimiter.IsGeneralApiThrottled(ip, out _))
            return StatusCode(429, new { error = "Too many requests. Please wait before trying again." });

        var (jwt, newRefreshToken, error) = await _auth.ExchangeRefreshTokenAsync(req.RefreshToken);
        if (error != null)
            return Unauthorized(new { error });

        return Ok(new { token = jwt, refreshToken = newRefreshToken });
    }

    // POST /api/auth/logout
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest req)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        if (_rateLimiter.IsGeneralApiThrottled(ip, out _))
            return StatusCode(429, new { error = "Too many requests. Please wait before trying again." });

        await _auth.RevokeRefreshTokenAsync(req.RefreshToken);
        return Ok(new { message = "Logged out successfully." });
    }

    // POST /api/auth/forgot-password
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest req)
    {
        var (throttled, _) = _rateLimiter.TryRecordPasswordResetRequest(req.Email);
        if (throttled)
            return StatusCode(429, new { error = "A reset code was already requested recently. Please wait before trying again." });

        var (found, _) = await _auth.RequestPasswordResetAsync(req.Email);
        if (!found)
            return NotFound(new { error = "Email not found." });

        _rateLimiter.ClearResetCodeAttempts(req.Email);
        return Ok(new { message = "A reset code has been sent to your email address." });
    }

    // POST /api/auth/verify-reset-code
    [HttpPost("verify-reset-code")]
    public async Task<IActionResult> VerifyResetCode([FromBody] VerifyResetCodeRequest req)
    {
        var (throttled, _) = _rateLimiter.TryRecordResetCodeAttempt(req.Email);
        if (throttled)
            return StatusCode(429, new { error = "Too many incorrect attempts. Please request a new reset code." });

        var valid = await _auth.VerifyResetCodeAsync(req.Email, req.Code);
        if (!valid)
            return BadRequest(new { error = "Invalid or expired reset code." });

        return Ok(new { message = "Code verified. You may now reset your password." });
    }

    // POST /api/auth/reset-password
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req)
    {
        var (throttled, _) = _rateLimiter.TryRecordResetCodeAttempt(req.Email);
        if (throttled)
            return StatusCode(429, new { error = "Too many incorrect attempts. Please request a new reset code." });

        var (success, errors) = await _auth.ResetPasswordAsync(req.Email, req.Code, req.NewPassword);
        if (!success)
            return BadRequest(new { errors });

        return Ok(new { message = "Password reset successfully." });
    }

    // PUT /api/auth/profile
    [Authorize]
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest req)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        if (_rateLimiter.IsGeneralApiThrottled(ip, out _))
            return StatusCode(429, new { error = "Too many requests. Please wait before trying again." });

        var userId = User.FindFirstValue("sub") ?? "";
        var (user, error) = await _auth.UpdateProfileAsync(userId, req.FullName, req.Grade);
        if (user == null)
            return BadRequest(new { error });

        return Ok(await _auth.BuildUserSummaryAsync(user));
    }

    // POST /api/auth/change-password
    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        if (_rateLimiter.IsGeneralApiThrottled(ip, out _))
            return StatusCode(429, new { error = "Too many requests. Please wait before trying again." });

        var userId = User.FindFirstValue("sub") ?? "";
        var (success, errors) = await _auth.ChangePasswordAsync(userId, req.CurrentPassword, req.NewPassword);
        if (!success)
            return BadRequest(new { errors });

        return Ok(new { message = "Password changed successfully." });
    }

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
    [MaxLength(100)] string? TeacherRegistrationCode,
    [MaxLength(100)] string? ClassJoinCode);

public record RefreshRequest([Required, MaxLength(256)] string RefreshToken);

public record ForgotPasswordRequest([Required, MaxLength(254)] string Email);

public record VerifyResetCodeRequest(
    [Required, MaxLength(254)] string Email,
    [Required, MaxLength(6)]   string Code);

public record ResetPasswordRequest(
    [Required, MaxLength(254)] string Email,
    [Required, MaxLength(6)]   string Code,
    [Required, MaxLength(128)] string NewPassword);

public record UpdateProfileRequest(
    [Required, MaxLength(150)] string FullName,
    int? Grade);

public record ChangePasswordRequest(
    [Required, MaxLength(128)] string CurrentPassword,
    [Required, MaxLength(128)] string NewPassword);
