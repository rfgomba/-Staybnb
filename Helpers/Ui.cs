using Staybnb.Models;

namespace Staybnb.Helpers;

/// <summary>Small display helpers shared by the views.</summary>
public static class Ui
{
    public static string Rand(decimal amount, int decimals = 0) =>
        "R " + amount.ToString(decimals == 0 ? "N0" : "N2");

    public static string BookingBadge(BookingStatus status) => status switch
    {
        BookingStatus.Pending => "bg-warning text-dark",
        BookingStatus.Approved => "bg-primary",
        BookingStatus.CheckedIn => "bg-success",
        BookingStatus.Completed => "bg-secondary",
        BookingStatus.Rejected => "bg-danger",
        BookingStatus.Cancelled => "bg-dark",
        _ => "bg-light text-dark"
    };

    public static string ApplicationBadge(ApplicationStatus? status) => status switch
    {
        ApplicationStatus.Approved => "bg-success",
        ApplicationStatus.Rejected => "bg-danger",
        _ => "bg-warning text-dark"
    };

    public static string ActivityBadge(ActivityType type) => type switch
    {
        ActivityType.Security => "bg-danger",
        ActivityType.RoleChange => "bg-primary",
        ActivityType.Login or ActivityType.Logout => "bg-secondary",
        ActivityType.Registration => "bg-info text-dark",
        ActivityType.HostApplication => "bg-warning text-dark",
        ActivityType.BookingStatusChange => "bg-success",
        ActivityType.CheckIn => "bg-success",
        _ => "bg-light text-dark border"
    };

    public static string RoleBadge(string role) => role switch
    {
        AppRoles.SuperAdmin => "bg-dark",
        AppRoles.Admin => "bg-primary",
        AppRoles.Host => "bg-danger",
        _ => "bg-light text-dark border"
    };

    public static string Initials(ApplicationUser? user) =>
        user == null ? "?" : $"{user.FirstName.FirstOrDefault()}{user.LastName.FirstOrDefault()}".ToUpper();
}
