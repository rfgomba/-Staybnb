using Microsoft.AspNetCore.Identity;
using Staybnb.Models;

namespace Staybnb.Services;

public interface IRoleService
{
    Task<IdentityResult> PromoteToHostAsync(ApplicationUser user, string performedById);
    Task<IdentityResult> PromoteToAdminAsync(ApplicationUser user, string performedById);
}

/// <summary>
/// Handles the Guest to Host (and Guest to Admin) transition.
/// A user is either a Guest or a Host, never both, so Guest is always removed.
/// </summary>
public class RoleService : IRoleService
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly IActivityLogger _log;
    private readonly INotificationService _notify;

    public RoleService(UserManager<ApplicationUser> users, IActivityLogger log, INotificationService notify)
    {
        _users = users;
        _log = log;
        _notify = notify;
    }

    public Task<IdentityResult> PromoteToHostAsync(ApplicationUser user, string performedById) =>
        SwapAsync(user, AppRoles.Host, performedById,
            NotificationType.HostApplicationApproved,
            "Host Application Approved",
            "Congratulations! You are now a Host. Your Host Dashboard is ready.",
            "/Host/Dashboard");

    public Task<IdentityResult> PromoteToAdminAsync(ApplicationUser user, string performedById) =>
        SwapAsync(user, AppRoles.Admin, performedById,
            NotificationType.General,
            "You are now an Admin",
            "A SuperAdmin has given you Administrator access.",
            "/Admin/Dashboard");

    private async Task<IdentityResult> SwapAsync(ApplicationUser user, string newRole, string performedById,
        NotificationType type, string title, string body, string link)
    {
        // 1. Add the new role
        if (!await _users.IsInRoleAsync(user, newRole))
        {
            var added = await _users.AddToRoleAsync(user, newRole);
            if (!added.Succeeded) return added;
        }

        // 2. Remove the Guest role
        if (await _users.IsInRoleAsync(user, AppRoles.Guest))
        {
            var removed = await _users.RemoveFromRoleAsync(user, AppRoles.Guest);
            if (!removed.Succeeded) return removed;
        }

        await _log.LogAsync(performedById, $"RoleChange: {user.Email} Guest to {newRole}", ActivityType.RoleChange);
        await _notify.NotifyAsync(user.Id, title, body, type, link);

        return IdentityResult.Success;
    }
}
