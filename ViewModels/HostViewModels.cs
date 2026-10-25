using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Staybnb.Models;

namespace Staybnb.ViewModels;

public class HostDashboardViewModel
{
    public string HostName { get; set; } = "Host";
    public int TotalProperties { get; set; }
    public int ActiveProperties { get; set; }
    public int TotalBookings { get; set; }
    public int BookingsThisMonth { get; set; }
    public decimal TotalEarnings { get; set; }
    public decimal EarningsThisMonth { get; set; }
    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public int PendingRequests { get; set; }
    public double OccupancyRate { get; set; }
    public decimal AverageNightlyRate { get; set; }
    public decimal AverageBookingValue { get; set; }
    public int MissingCheckInSetup { get; set; }
    public List<Booking> RecentBookings { get; set; } = new();
}

public class HostPropertyListItem
{
    public HostProperty Property { get; set; } = null!;
    public ApplicationStatus? Status { get; set; }
    public bool IsApproved => Status == ApplicationStatus.Approved;
}

/// <summary>Edit form reuses every field and rule from the application form.</summary>
public class PropertyEditViewModel : HostApplicationViewModel
{
    public int Id { get; set; }
    public List<int> RemoveImageIds { get; set; } = new();

    [ValidateNever]
    public List<PropertyImage> ExistingImages { get; set; } = new();
}

public class CheckInSetupViewModel
{
    public int PropertyId { get; set; }

    [ValidateNever]
    public string PropertyTitle { get; set; } = string.Empty;

    [Required, StringLength(200), Display(Name = "Process title")]
    public string Title { get; set; } = string.Empty;

    public List<CheckInStep> Steps { get; set; } = new();
}
