using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.ViewModels;

namespace Staybnb.Controllers;

[Authorize]
public class NotificationsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public NotificationsController(ApplicationDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    private string UserId => _users.GetUserId(User)!;

    public async Task<IActionResult> Index(string filter = "all", int? selectedId = null)
    {
        var mine = _db.Notifications.Where(n => n.UserId == UserId);
        var todayStart = DateTime.Today.ToUniversalTime();

        var filtered = filter switch
        {
            "unread" => mine.Where(n => !n.IsRead),
            "read" => mine.Where(n => n.IsRead),
            "today" => mine.Where(n => n.CreatedAt >= todayStart),
            _ => mine
        };

        var model = new NotificationsViewModel
        {
            Filter = filter,
            AllCount = await mine.CountAsync(),
            UnreadCount = await mine.CountAsync(n => !n.IsRead),
            ReadCount = await mine.CountAsync(n => n.IsRead),
            TodayCount = await mine.CountAsync(n => n.CreatedAt >= todayStart),
            Items = await filtered.OrderByDescending(n => n.CreatedAt).Take(100).ToListAsync()
        };

        // Selecting a notification marks it as read
        if (selectedId.HasValue)
        {
            model.Selected = await mine.FirstOrDefaultAsync(n => n.Id == selectedId);
            if (model.Selected is { IsRead: false })
            {
                model.Selected.IsRead = true;
                await _db.SaveChangesAsync();
                model.UnreadCount--;
                model.ReadCount++;
            }
        }

        return View(model);
    }

    // "Take Action": mark read, then go to the page the notification is about
    public async Task<IActionResult> Open(int id)
    {
        var n = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == UserId);
        if (n == null) return NotFound();

        if (!n.IsRead)
        {
            n.IsRead = true;
            await _db.SaveChangesAsync();
        }

        return !string.IsNullOrEmpty(n.Link) && Url.IsLocalUrl(n.Link)
            ? LocalRedirect(n.Link)
            : RedirectToAction(nameof(Index), new { selectedId = id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkUnread(int id, string filter = "all")
    {
        var n = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == UserId);
        if (n == null) return NotFound();
        n.IsRead = false;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index), new { filter });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead(string filter = "all")
    {
        await _db.Notifications
            .Where(n => n.UserId == UserId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
        TempData["Success"] = "All notifications marked as read.";
        return RedirectToAction(nameof(Index), new { filter });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, string filter = "all")
    {
        var n = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == UserId);
        if (n == null) return NotFound();
        _db.Notifications.Remove(n);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Notification deleted.";
        return RedirectToAction(nameof(Index), new { filter });
    }
}
