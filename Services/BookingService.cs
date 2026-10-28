using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;

namespace Staybnb.Services;

public record BookingQuote(int Nights, decimal NightlyRate, decimal Subtotal, decimal CleaningFee, decimal ServiceFee, decimal Total);

public interface IBookingService
{
    string? Validate(HostProperty property, DateTime checkIn, DateTime checkOut, int guests);
    BookingQuote CalculateQuote(HostProperty property, DateTime checkIn, DateTime checkOut);
    Task<bool> IsAvailableAsync(int propertyId, DateTime checkIn, DateTime checkOut, int? ignoreBookingId = null);
    Task<List<Booking>> UpcomingConfirmedAsync(int propertyId);
}

/// <summary>Single source of truth for booking rules and pricing.</summary>
public class BookingService : IBookingService
{
    public const decimal ServiceFeeRate = 0.12m;   // 12% platform service fee
    public const int MaxNights = 30;

    // Only confirmed stays block the calendar; pending requests do not
    private static readonly BookingStatus[] Blocking = { BookingStatus.Approved, BookingStatus.CheckedIn };

    private readonly ApplicationDbContext _db;
    public BookingService(ApplicationDbContext db) => _db = db;

    public string? Validate(HostProperty property, DateTime checkIn, DateTime checkOut, int guests)
    {
        if (!property.IsActive) return "This property is not accepting bookings.";
        if (checkIn.Date < DateTime.Today) return "Check-in cannot be in the past.";
        if (checkOut.Date <= checkIn.Date) return "Check-out must be after check-in.";
        if ((checkOut.Date - checkIn.Date).Days > MaxNights) return $"Stays are limited to {MaxNights} nights.";
        if (guests < 1 || guests > property.MaxGuests) return $"This place allows 1 to {property.MaxGuests} guests.";
        return null;
    }

    public BookingQuote CalculateQuote(HostProperty property, DateTime checkIn, DateTime checkOut)
    {
        var nights = (checkOut.Date - checkIn.Date).Days;
        var subtotal = property.PricePerNight * nights;
        var serviceFee = Math.Round(subtotal * ServiceFeeRate, 2, MidpointRounding.AwayFromZero);

        // TotalPrice = (PricePerNight x Nights) + CleaningFee + ServiceFee
        var total = subtotal + property.CleaningFee + serviceFee;

        return new BookingQuote(nights, property.PricePerNight, subtotal, property.CleaningFee, serviceFee, total);
    }

    public async Task<bool> IsAvailableAsync(int propertyId, DateTime checkIn, DateTime checkOut, int? ignoreBookingId = null)
    {
        var start = checkIn.Date;
        var end = checkOut.Date;
        return !await _db.Bookings.AnyAsync(b =>
            b.PropertyId == propertyId &&
            b.Id != ignoreBookingId &&
            Blocking.Contains(b.Status) &&
            b.CheckInDate < end && start < b.CheckOutDate);   // date ranges overlap
    }

    public Task<List<Booking>> UpcomingConfirmedAsync(int propertyId) =>
        _db.Bookings
            .Where(b => b.PropertyId == propertyId && Blocking.Contains(b.Status) && b.CheckOutDate >= DateTime.Today)
            .OrderBy(b => b.CheckInDate)
            .ToListAsync();
}
