using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.Services;
using Staybnb.ViewModels;

namespace Staybnb.Controllers;

/// <summary>Host side of the booking workflow: Pending to Approved to CheckedIn to Completed.</summary>
[Authorize(Roles = AppRoles.Host)]
public class HostBookingsController : Controller
{
    private static readonly string[] Tabs = { "Pending", "Approved", "CheckedIn", "Completed", "Rejected", "Cancelled", "All" };

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IBookingService _bookings;
    private readonly IActivityLogger _log;
    private readonly INotificationService _notify;

    public HostBookingsController(ApplicationDbContext db, UserManager<ApplicationUser> users, IBookingService bookings,
        IActivityLogger log, INotificationService notify)
    {
        _db = db;
        _users = users;
        _bookings = bookings;
        _log = log;
        _notify = notify;
    }

    private string UserId => _users.GetUserId(User)!;

    public async Task<IActionResult> Index(string status = "Pending")
    {
        if (!Tabs.Contains(status)) status = "Pending";

        var all = await _db.Bookings
            .Where(b => b.Property!.HostId == UserId)
            .Include(b => b.Guest)
            .Include(b => b.Property).ThenInclude(p => p!.Images)
            .Include(b => b.Payment)
            .Include(b => b.GuestCheckIn)
            .AsSplitQuery()
            .OrderBy(b => b.CheckInDate)
            .ToListAsync();

        return View(new HostBookingsViewModel
        {
            Status = status,
            Counts = Tabs.ToDictionary(t => t, t => t == "All" ? all.Count : all.Count(b => b.Status.ToString() == t)),
            Bookings = status == "All" ? all : all.Where(b => b.Status.ToString() == status).ToList()
        });
    }

    public async Task<IActionResult> Details(int id)
    {
        var booking = await LoadOwnedAsync(id);
        return booking == null ? NotFound() : View(booking);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id)
    {
        var booking = await LoadOwnedAsync(id);
        if (booking == null) return NotFound();
        if (booking.Status != BookingStatus.Pending) return Fail(id, "Only pending requests can be approved.");

        if (!await _bookings.IsAvailableAsync(booking.PropertyId, booking.CheckInDate, booking.CheckOutDate, booking.Id))
            return Fail(id, "These dates overlap with a booking you already approved.");

        await ChangeStatusAsync(booking, BookingStatus.Approved, NotificationType.BookingApproved, "Booking Approved",
            $"Your stay at '{booking.Property!.Title}' on {booking.CheckInDate:dd MMM} to {booking.CheckOutDate:dd MMM} was approved. Complete payment and check-in.");

        TempData["Success"] = $"Booking #{id} approved.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string? reason)
    {
        var booking = await LoadOwnedAsync(id);
        if (booking == null) return NotFound();
        if (booking.Status != BookingStatus.Pending) return Fail(id, "Only pending requests can be rejected.");

        if (booking.Payment != null) booking.Payment.Status = PaymentStatus.Failed;

        var note = string.IsNullOrWhiteSpace(reason) ? "" : $" Reason: {reason.Trim()}";
        await ChangeStatusAsync(booking, BookingStatus.Rejected, NotificationType.BookingRejected, "Booking Rejected",
            $"Your request for '{booking.Property!.Title}' was not accepted.{note}");

        TempData["Success"] = $"Booking #{id} rejected.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyDocument(int id, int documentId, bool approve)
    {
        var booking = await LoadOwnedAsync(id);
        var checkIn = booking?.GuestCheckIn;
        var doc = checkIn?.Documents.FirstOrDefault(d => d.Id == documentId);
        if (booking == null || checkIn == null || doc == null) return NotFound();

        doc.Status = approve ? DocumentStatus.Verified : DocumentStatus.Rejected;

        // Roll the document results up to the check-in
        checkIn.Status = checkIn.Documents.Any(d => d.Status == DocumentStatus.Rejected) ? CheckInStatus.Rejected
                       : checkIn.Documents.All(d => d.Status == DocumentStatus.Verified) ? CheckInStatus.Verified
                       : CheckInStatus.Submitted;
        await _db.SaveChangesAsync();

        await _log.LogAsync(UserId, $"Document{doc.Status}: {doc.DocumentType} for booking #{id}", ActivityType.CheckIn);

        if (!approve)
            await _notify.NotifyAsync(booking.GuestId, "Document Rejected",
                $"Your {doc.DocumentType} for '{booking.Property!.Title}' was rejected. Please upload it again.",
                NotificationType.CheckInUpdate, $"/Guest/CheckIn/{id}");
        else if (checkIn.Status == CheckInStatus.Verified)
            await _notify.NotifyAsync(booking.GuestId, "Documents Verified",
                $"Your check-in documents for '{booking.Property!.Title}' are verified.",
                NotificationType.CheckInUpdate, $"/Guest/Booking/{id}");

        TempData["Success"] = $"{doc.DocumentType} marked as {doc.Status}.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckInGuest(int id)
    {
        var booking = await LoadOwnedAsync(id);
        if (booking == null) return NotFound();

        if (booking.Status != BookingStatus.Approved) return Fail(id, "Only approved bookings can be checked in.");
        if (booking.Payment?.Status != PaymentStatus.Paid) return Fail(id, "The guest has not paid yet.");
        if (booking.GuestCheckIn?.Status != CheckInStatus.Verified) return Fail(id, "Verify all guest documents first.");

        await ChangeStatusAsync(booking, BookingStatus.CheckedIn, NotificationType.CheckInUpdate, "You're Checked In",
            $"Welcome to '{booking.Property!.Title}'. Enjoy your stay!");

        TempData["Success"] = $"Guest checked in for booking #{id}.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int id)
    {
        var booking = await LoadOwnedAsync(id);
        if (booking == null) return NotFound();
        if (booking.Status != BookingStatus.CheckedIn) return Fail(id, "Only checked-in stays can be completed.");

        await ChangeStatusAsync(booking, BookingStatus.Completed, NotificationType.General, "Stay Completed",
            $"Thanks for staying at '{booking.Property!.Title}'. We hope to see you again.");

        TempData["Success"] = $"Booking #{id} marked as completed.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // ---------- Helpers ----------

    private Task<Booking?> LoadOwnedAsync(int id) =>
        _db.Bookings
            .Include(b => b.Guest)
            .Include(b => b.Property).ThenInclude(p => p!.Images)
            .Include(b => b.Payment)
            .Include(b => b.GuestCheckIn).ThenInclude(c => c!.Documents)
            .Include(b => b.GuestCheckIn).ThenInclude(c => c!.CheckInProcess)
            .AsSplitQuery()
            .FirstOrDefaultAsync(b => b.Id == id && b.Property!.HostId == UserId);

    private async Task ChangeStatusAsync(Booking booking, BookingStatus newStatus, NotificationType type, string title, string body)
    {
        var previous = booking.Status;
        booking.Status = newStatus;
        await _db.SaveChangesAsync();

        await _log.LogAsync(UserId, $"BookingStatusChange: #{booking.Id} {previous} to {newStatus}", ActivityType.BookingStatusChange);
        await _notify.NotifyAsync(booking.GuestId, title, body, type, $"/Guest/Booking/{booking.Id}");
    }

    private IActionResult Fail(int id, string message)
    {
        TempData["Error"] = message;
        return RedirectToAction(nameof(Details), new { id });
    }
}
