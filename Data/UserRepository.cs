using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StudyAssistant.Models;

namespace StudyAssistant.Data;

public class UserRepository : IUserRepository
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UserRepository(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<ApplicationUser?> GetByIdAsync(string id) =>
        await _userManager.FindByIdAsync(id);

    public async Task<ApplicationUser?> GetByEmailAsync(string email) =>
        await _userManager.FindByEmailAsync(email);

    public async Task<ApplicationUser?> GetByUsernameAsync(string username) =>
        await _userManager.FindByNameAsync(username);

    public async Task<IReadOnlyList<ApplicationUser>> GetAllAsync() =>
        await _userManager.Users.AsNoTracking().ToListAsync();

    public async Task<IReadOnlyList<ApplicationUser>> GetByRoleAsync(UserRole role) =>
        await _userManager.Users.AsNoTracking().Where(u => u.Role == role).ToListAsync();

    public async Task<(bool Success, IReadOnlyList<string> Errors)> CreateAsync(ApplicationUser user, string password)
    {
        var result = await _userManager.CreateAsync(user, password);
        return (result.Succeeded, result.Errors.Select(e => e.Description).ToList());
    }

    public async Task<bool> UpdateAsync(ApplicationUser user)
    {
        var result = await _userManager.UpdateAsync(user);
        return result.Succeeded;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return false;
        var result = await _userManager.DeleteAsync(user);
        return result.Succeeded;
    }

    public async Task<bool> CheckPasswordAsync(ApplicationUser user, string password) =>
        await _userManager.CheckPasswordAsync(user, password);

    public async Task<string> GeneratePasswordResetTokenAsync(ApplicationUser user) =>
        await _userManager.GeneratePasswordResetTokenAsync(user);

    public async Task<(bool Success, IReadOnlyList<string> Errors)> ResetPasswordAsync(
        ApplicationUser user, string token, string newPassword)
    {
        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
        return (result.Succeeded, result.Errors.Select(e => e.Description).ToList());
    }
}
