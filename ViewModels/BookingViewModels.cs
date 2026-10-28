using System.ComponentModel.DataAnnotations;
using Staybnb.Models;

namespace Staybnb.ViewModels;

public class PropertySearchViewModel
{
    public string? City { get; set; }
    public string? Type { get; set; }
    public int? Guests { get; set; }
    public List<string> Cities { get; set; } = new();
    public List<HostProperty> Results { get; set; } = new();
}

public class PropertyDetailsViewModel
{
    public HostProperty Property { get; set; } = null!;
    public double AverageRating { get; set; }
    public List<Booking> UnavailableRanges { get; set; } = new();
    public bool IsGuest { get; set; }
    public bool IsSignedIn { get; set; }
    public DateTime DefaultCheckIn { get; set; }
    public DateTime DefaultCheckOut { get; set; }
}

public class BookingRequestModel
{
    [Required] public int PropertyId { get; set; }
    [Required, DataType(DataType.Date)] public DateTime CheckIn { get; set; }
    [Required, DataType(DataType.Date)] public DateTime CheckOut { get; set; }
    [Range(1, 50)] public int Guests { get; set; } = 1;
}

public class GuestCheckInViewModel
{
    public Booking Booking { get; set; } = null!;
    public GuestCheckIn CheckIn { get; set; } = null!;
    public string ProcessTitle { get; set; } = string.Empty;
    public List<CheckInStep> Steps { get; set; } = new();
    public bool IsLocked => CheckIn.Status == CheckInStatus.Verified;
}

public class HostBookingsViewModel
{
    public string Status { get; set; } = "Pending";
    public Dictionary<string, int> Counts { get; set; } = new();
    public List<Booking> Bookings { get; set; } = new();
}
