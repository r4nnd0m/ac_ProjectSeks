using AuthApi.Data;
using AuthApi.Dtos;
using AuthApi.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sims.Contracts;

namespace AuthApi.Services;

public class UserService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AuthDbContext _db;

    public UserService(UserManager<ApplicationUser> userManager, AuthDbContext db)
    {
        _userManager = userManager;
        _db = db;
    }

    public async Task<UserResult> CreateAsync(CreateUserRequest request)
    {
        if (!Roles.All.Contains(request.Role))
        {
            return new UserResult(false, null, ["Unbekannte Rolle"]);
        }

        if (!await _db.Teams.AnyAsync(t => t.Id == request.TeamId))
        {
            return new UserResult(false, null, ["Unbekannte TeamID"]);
        }

        ApplicationUser user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            EmailConfirmed = true,
            TeamId = request.TeamId
        };

        IdentityResult created = await _userManager.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            string[] errors = created.Errors.Select(e => e.Description).ToArray();
            return new UserResult(false, null, errors);
        }

        IdentityResult roleResult = await _userManager.AddToRoleAsync(user, request.Role);
        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            string[] errors = roleResult.Errors.Select(e => e.Description).ToArray();
            return new UserResult(false, null, errors);
        }

        UserResponse response = new UserResponse(
            user.Id, user.Email!, request.Role, user.TeamId, user.IsActive, user.TwoFactorEnabled);

        return new UserResult(true, response, []);
    }

    public async Task<List<UserResponse>> ListAsync()
    {
        return await (
            from u in _db.Users
            join ur in _db.UserRoles on u.Id equals ur.UserId
            join r in _db.Roles on ur.RoleId equals r.Id
            orderby u.Email
            select new UserResponse(u.Id, u.Email!, r.Name!, u.TeamId, u.IsActive, u.TwoFactorEnabled))
            .ToListAsync();
    }

    public async Task<DeactivateOutcome> DeactivateAsync(Guid id, Guid currentUserId)
    {
        if (id == currentUserId)
        {
            return DeactivateOutcome.CannotDeactivateSelf;
        }

        ApplicationUser? user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null)
        {
            return DeactivateOutcome.NotFound;
        }

        user.IsActive = false;
        await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        await _userManager.UpdateAsync(user);
        return DeactivateOutcome.Ok;
    }
}