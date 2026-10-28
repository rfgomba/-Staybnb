using Staybnb.Models;

namespace Staybnb.ViewModels;

public class ConversationSummary
{
    public ApplicationUser Other { get; set; } = null!;
    public Message? Last { get; set; }
    public int Unread { get; set; }
}

public class InboxViewModel
{
    public string CurrentUserId { get; set; } = string.Empty;
    public List<ConversationSummary> Conversations { get; set; } = new();
    public ApplicationUser? With { get; set; }
    public List<Message> Thread { get; set; } = new();
    public bool CanSend { get; set; }
    public string? SupportUserId { get; set; }
}

public class NotificationsViewModel
{
    public string Filter { get; set; } = "all";
    public int AllCount { get; set; }
    public int UnreadCount { get; set; }
    public int ReadCount { get; set; }
    public int TodayCount { get; set; }
    public List<Notification> Items { get; set; } = new();
    public Notification? Selected { get; set; }
}

public class HeaderIconsViewModel
{
    public int UnreadMessages { get; set; }
    public int UnreadNotifications { get; set; }
    public List<Notification> Latest { get; set; } = new();
}
