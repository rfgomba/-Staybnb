using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Helpers;
using Staybnb.Models;
using Staybnb.Services;
using Staybnb.ViewModels;

namespace Staybnb.Controllers;

public class BookingsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IBookingService _bookings;
    private readonly IActivityLogger _log;
    private readonly INotificationService _notify;

    public BookingsController(ApplicationDbContext db, UserManager<ApplicationUser> users, IBookingService bookings,
        IActivityLogger log, INotificationService notify)
    {
        _db = db;
        _users = users;
        _bookings = bookings;
        _log = log;
        _notify = notify;
    }

    // Live price breakdown for the booking widget (same formula the server uses to save)
    [HttpGet, AllowAnonymous]
    public async Task<IActionResult> Quote(int propertyId, DateTime checkIn, DateTime checkOut, int guests = 1)
    {
        var property = await _db.HostProperties.FirstOrDefaultAsync(p => p.Id == propertyId);
        if (property == null) return NotFound();

        var error = _bookings.Validate(property, checkIn, checkOut, guests);
        if (error == null && !await _bookings.IsAvailableAsync(propertyId, checkIn, checkOut))
            error = "Those dates are already booked.";
        if (error != null) return Json(new { ok = false, error });

        var q = _bookings.CalculateQuote(property, checkIn, checkOut);
        return Json(new
        {
            ok = true,
            nights = q.Nights,
            nightsLine = $"{Ui.Rand(q.NightlyRate)} x {q.Nights} {(q.Nights == 1 ? "night" : "nights")}",
            subtotal = Ui.Rand(q.Subtotal),
            cleaningFee = Ui.Rand(q.CleaningFee),
            serviceFee = Ui.Rand(q.ServiceFee),
            total = Ui.Rand(q.Total, 2)
        });
    }

    [HttpPost, Authorize(Roles = AppRoles.Guest), ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BookingRequestModel model)
    {
        var property = await _db.HostProperties.FirstOrDefaultAsync(p => p.Id == model.PropertyId);
        if (property == null) return NotFound();

        var error = ModelState.IsValid ? _bookings.Validate(property, model.CheckIn, model.CheckOut, model.Guests) : "Please check your dates and guests.";
        if (error == null && !await _bookings.IsAvailableAsync(property.Id, model.CheckIn, model.CheckOut))
            error = "Those dates are already booked. Please choose different dates.";
        if (error != null)
        {
            TempData["Error"] = error;
            return RedirectToAction("View", "Properties", new { id = property.Id });
        }

        var guestId = _users.GetUserId(User)!;
        var quote = _bookings.CalculateQuote(property, model.CheckIn, model.CheckOut);

        var booking = new Booking
        {
            PropertyId = property.Id,
            GuestId = guestId,
            CheckInDate = model.CheckIn.Date,
            CheckOutDate = model.CheckOut.Date,
            NumberOfGuests = model.Guests,
            CleaningFee = quote.CleaningFee,
            ServiceFee = quote.ServiceFee,
            TotalPrice = quote.Total,
            Status = BookingStatus.Pending,
            Payment = new Payment { Amount = quote.Total, Status = PaymentStatus.Pending }
        };
        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync();

        await _log.LogAsync(guestId, $"BookingRequested: #{booking.Id} {property.Title}", ActivityType.BookingStatusChange);
        await _notify.NotifyAsync(property.HostId, "New Booking Request",
            $"{booking.CheckInDate:dd MMM} to {booking.CheckOutDate:dd MMM} at '{property.Title}' ({booking.NumberOfGuests} guests).",
            NotificationType.BookingRequested, $"/HostBookings/Details/{booking.Id}");

        TempData["Success"] = "Booking request sent. The host will review it shortly.";
        return RedirectToAction("Booking", "Guest", new { id = booking.Id });
    }
}
