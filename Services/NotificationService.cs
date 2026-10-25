using Staybnb.Data;
using Staybnb.Models;

namespace Staybnb.Services;

public interface INotificationService
{
    Task NotifyAsync(string userId, string title, string? body, NotificationType type, string? link = null);
}

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _db;
    public NotificationService(ApplicationDbContext db) => _db = db;

    public async Task NotifyAsync(string userId, string title, string? body, NotificationType type, string? link = null)
    {
        _db.Notifications.Add(new Notification
        {
            UserId = userId,
            Title = title,
            Body = body,
            Type = type,
            Link = link
        });
        await _db.SaveChangesAsync();
    }
}
