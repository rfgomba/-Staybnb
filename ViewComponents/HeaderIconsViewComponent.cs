using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.ViewModels;

namespace Staybnb.ViewComponents;

/// <summary>Envelope and bell icons with unread counts, shown in every layout.</summary>
public class HeaderIconsViewComponent : ViewComponent
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public HeaderIconsViewComponent(ApplicationDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var userId = _users.GetUserId(UserClaimsPrincipal);
        if (userId == null) return Content(string.Empty);

        return View(new HeaderIconsViewModel
        {
            UnreadMessages = await _db.Messages.CountAsync(m => m.ReceiverId == userId && !m.IsRead),
            UnreadNotifications = await _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead),
            Latest = await _db.Notifications.Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt).Take(5).ToListAsync()
        });
    }
}
