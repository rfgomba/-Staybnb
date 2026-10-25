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
}
