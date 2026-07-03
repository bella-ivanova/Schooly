using StudyAssistant.Models;

namespace StudyAssistant.Data;

public interface IUserRepository
{
    Task<ApplicationUser?> GetByIdAsync(string id);
    Task<ApplicationUser?> GetByEmailAsync(string email);
    Task<ApplicationUser?> GetByUsernameAsync(string username);
    Task<IReadOnlyList<ApplicationUser>> GetAllAsync();
    Task<IReadOnlyList<ApplicationUser>> GetByRoleAsync(UserRole role);

    // Returns true if the user was created; false if the email/username already exists.
    Task<(bool Success, IReadOnlyList<string> Errors)> CreateAsync(ApplicationUser user, string password);

    Task<bool> UpdateAsync(ApplicationUser user);
    Task<bool> DeleteAsync(string id);

    Task<bool> CheckPasswordAsync(ApplicationUser user, string password);

    // Password reset
    Task<string> GeneratePasswordResetTokenAsync(ApplicationUser user);
    Task<(bool Success, IReadOnlyList<string> Errors)> ResetPasswordAsync(ApplicationUser user, string token, string newPassword);
    Task<(bool Success, IReadOnlyList<string> Errors)> ChangePasswordDirectlyAsync(ApplicationUser user, string newPassword);
}
