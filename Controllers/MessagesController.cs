using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.Services;
using Staybnb.ViewModels;

namespace Staybnb.Controllers;

/// <summary>Database-backed inbox (no SignalR): conversations grouped by the other person.</summary>
[Authorize]
public class MessagesController : Controller
{
    private const int MaxLength = 2000;

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly INotificationService _notify;

    public MessagesController(ApplicationDbContext db, UserManager<ApplicationUser> users, INotificationService notify)
    {
        _db = db;
        _users = users;
        _notify = notify;
    }

    private string UserId => _users.GetUserId(User)!;

    public async Task<IActionResult> Index(string? with)
    {
        var me = UserId;

        var mine = await _db.Messages
            .Where(m => m.SenderId == me || m.ReceiverId == me)
            .Include(m => m.Sender)
            .Include(m => m.Receiver)
            .OrderByDescending(m => m.Timestamp)
            .ToListAsync();

        var conversations = mine
            .GroupBy(m => m.SenderId == me ? m.ReceiverId : m.SenderId)
            .Select(g =>
            {
                var last = g.First();
                return new ConversationSummary
                {
                    Other = last.SenderId == me ? last.Receiver! : last.Sender!,
                    Last = last,
                    Unread = g.Count(m => m.ReceiverId == me && !m.IsRead)
                };
            })
            .ToList();

        with ??= conversations.FirstOrDefault()?.Other.Id;

        var model = new InboxViewModel
        {
            CurrentUserId = me,
            Conversations = conversations,
            SupportUserId = (await _users.GetUsersInRoleAsync(AppRoles.SuperAdmin)).FirstOrDefault()?.Id
        };

        if (with != null && with != me)
        {
            var other = await _users.FindByIdAsync(with);
            if (other != null)
            {
                model.With = other;
                model.CanSend = await CanMessageAsync(me, other);
                model.Thread = mine
                    .Where(m => m.SenderId == with || m.ReceiverId == with)
                    .OrderBy(m => m.Timestamp)
                    .ToList();

                // Opening a conversation marks its incoming messages as read
                var unread = model.Thread.Where(m => m.ReceiverId == me && !m.IsRead).ToList();
                if (unread.Any())
                {
                    unread.ForEach(m => m.IsRead = true);
                    await _db.SaveChangesAsync();
                    conversations.First(c => c.Other.Id == with).Unread = 0;
                }

                // New conversation started from a "Contact" button
                if (conversations.All(c => c.Other.Id != with))
                    conversations.Insert(0, new ConversationSummary { Other = other });
            }
        }

        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(string receiverId, string? content)
    {
        var me = UserId;
        var receiver = await _users.FindByIdAsync(receiverId);
        if (receiver == null || receiver.Id == me) return NotFound();

        var text = content?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            TempData["Error"] = "Type a message first.";
            return RedirectToAction(nameof(Index), new { with = receiverId });
        }
        if (text.Length > MaxLength) text = text[..MaxLength];

        if (!await CanMessageAsync(me, receiver))
        {
            TempData["Error"] = "You can't message this user.";
            return RedirectToAction(nameof(Index));
        }

        _db.Messages.Add(new Message { SenderId = me, ReceiverId = receiverId, Content = text });
        await _db.SaveChangesAsync();

        var sender = await _users.GetUserAsync(User);
        var preview = text.Length > 80 ? text[..80] + "..." : text;
        await _notify.NotifyAsync(receiverId, "New Message", $"{sender?.FullName}: {preview}",
            NotificationType.NewMessage, $"/Messages?with={me}");

        return RedirectToAction(nameof(Index), new { with = receiverId });
    }

    /// <summary>
    /// Who may start or continue a conversation: staff can message anyone; anyone can contact staff
    /// or a host with a live listing; booking parties can message each other; anyone can reply.
    /// </summary>
    private async Task<bool> CanMessageAsync(string senderId, ApplicationUser receiver)
    {
        if (senderId == receiver.Id) return false;
        if (User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.SuperAdmin)) return true;
        if (await _users.IsInRoleAsync(receiver, AppRoles.Admin) || await _users.IsInRoleAsync(receiver, AppRoles.SuperAdmin)) return true;
        if (await _db.Messages.AnyAsync(m => m.SenderId == receiver.Id && m.ReceiverId == senderId)) return true;
        if (await _db.HostProperties.AnyAsync(p => p.HostId == receiver.Id && p.IsActive)) return true;
        return await _db.Bookings.AnyAsync(b =>
            (b.GuestId == senderId && b.Property!.HostId == receiver.Id) ||
            (b.GuestId == receiver.Id && b.Property!.HostId == senderId));
    }
}
