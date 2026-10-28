using Staybnb.Models;

namespace Staybnb.ViewModels;

public class AdminDashboardViewModel
{
    public int TotalUsers { get; set; }
    public int NewUsersThisMonth { get; set; }
    public int PendingApplications { get; set; }
    public int ActiveProperties { get; set; }
    public int TotalProperties { get; set; }
    public decimal PlatformRevenue { get; set; }
    public decimal GrossBookingValue { get; set; }
    public int TotalBookings { get; set; }
    public int PendingBookings { get; set; }
    public int ApprovedHosts { get; set; }
    public List<ActivityLog> RecentActivity { get; set; } = new();
    public List<HostApplication> RecentApplications { get; set; } = new();
    public List<Booking> RecentBookings { get; set; } = new();
}

public class AdminUserRow
{
    public ApplicationUser User { get; set; } = null!;
    public List<string> Roles { get; set; } = new();
    public bool IsLocked { get; set; }
    public int Bookings { get; set; }
    public int Properties { get; set; }
    public bool IsGuest => Roles.Contains(AppRoles.Guest);
    public bool IsStaff => Roles.Contains(AppRoles.Admin) || Roles.Contains(AppRoles.SuperAdmin);
}

public class AdminUsersViewModel
{
    public string? Search { get; set; }
    public string? Role { get; set; }
    public bool IsSuperAdmin { get; set; }
    public string CurrentUserId { get; set; } = string.Empty;
    public List<AdminUserRow> Users { get; set; } = new();
}

public class AdminBookingsViewModel
{
    public string Status { get; set; } = "All";
    public Dictionary<string, int> Counts { get; set; } = new();
    public List<Booking> Bookings { get; set; } = new();
}

public class AdminPropertyRow
{
    public HostProperty Property { get; set; } = null!;
    public ApplicationStatus? Status { get; set; }
}

public class ActivityLogFilterViewModel
{
    public string? Type { get; set; }
    public string? Search { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Page { get; set; } = 1;
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
    public List<ActivityLog> Logs { get; set; } = new();
}
