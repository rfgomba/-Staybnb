using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Staybnb.Models;

public class Booking
{
    public int Id { get; set; }

    public int PropertyId { get; set; }
    public HostProperty? Property { get; set; }

    [Required] public string GuestId { get; set; } = string.Empty;
    public ApplicationUser? Guest { get; set; }

    [DataType(DataType.Date)] public DateTime CheckInDate { get; set; }
    [DataType(DataType.Date)] public DateTime CheckOutDate { get; set; }
    [Range(1, 50)] public int NumberOfGuests { get; set; } = 1;

    [Column(TypeName = "decimal(18,2)")] public decimal CleaningFee { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal ServiceFee { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal TotalPrice { get; set; }

    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [NotMapped] public int Nights => (CheckOutDate.Date - CheckInDate.Date).Days;

    public Payment? Payment { get; set; }
    public GuestCheckIn? GuestCheckIn { get; set; }
}
