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

[Authorize(Roles = AppRoles.Guest)]
public class GuestController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IFileStorageService _files;
    private readonly IActivityLogger _log;
    private readonly INotificationService _notify;

    public GuestController(ApplicationDbContext db, UserManager<ApplicationUser> users, IFileStorageService files,
        IActivityLogger log, INotificationService notify)
    {
        _db = db;
        _users = users;
        _files = files;
        _log = log;
        _notify = notify;
    }

    private string UserId => _users.GetUserId(User)!;

    // ---------- Trips ----------

    public async Task<IActionResult> Trips()
    {
        var bookings = await _db.Bookings
            .Where(b => b.GuestId == UserId)
            .Include(b => b.Property).ThenInclude(p => p!.Images)
            .Include(b => b.Payment)
            .Include(b => b.GuestCheckIn)
            .AsSplitQuery()
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();
        return View(bookings);
    }

    public async Task<IActionResult> Booking(int id)
    {
        var booking = await LoadBookingAsync(id);
        return booking == null ? NotFound() : View(booking);
    }

    // Simulated payment: no card details are collected or stored
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(int id)
    {
        var booking = await LoadBookingAsync(id);
        if (booking == null) return NotFound();

        if (booking.Status != BookingStatus.Approved || booking.Payment?.Status == PaymentStatus.Paid)
        {
            TempData["Error"] = "This booking cannot be paid right now.";
            return RedirectToAction(nameof(Booking), new { id });
        }

        booking.Payment ??= new Payment();
        booking.Payment.Amount = booking.TotalPrice;
        booking.Payment.Status = PaymentStatus.Paid;
        await _db.SaveChangesAsync();

        await _log.LogAsync(UserId, $"PaymentCompleted: booking #{id} {Ui.Rand(booking.TotalPrice, 2)}", ActivityType.BookingStatusChange);
        await _notify.NotifyAsync(booking.Property!.HostId, "Payment Received",
            $"{Ui.Rand(booking.TotalPrice, 2)} received for '{booking.Property.Title}'.",
            NotificationType.General, $"/HostBookings/Details/{id}");

        TempData["Success"] = "Payment successful. You can now complete your check-in.";
        return RedirectToAction(nameof(Booking), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var booking = await LoadBookingAsync(id);
        if (booking == null) return NotFound();

        if (booking.Status is not (BookingStatus.Pending or BookingStatus.Approved))
        {
            TempData["Error"] = "This booking can no longer be cancelled.";
            return RedirectToAction(nameof(Booking), new { id });
        }

        var previous = booking.Status;
        booking.Status = BookingStatus.Cancelled;
        if (booking.Payment != null)
            booking.Payment.Status = booking.Payment.Status == PaymentStatus.Paid ? PaymentStatus.Refunded : PaymentStatus.Failed;
        await _db.SaveChangesAsync();

        await _log.LogAsync(UserId, $"BookingStatusChange: #{id} {previous} to Cancelled", ActivityType.BookingStatusChange);
        await _notify.NotifyAsync(booking.Property!.HostId, "Booking Cancelled",
            $"The guest cancelled {booking.CheckInDate:dd MMM} to {booking.CheckOutDate:dd MMM} at '{booking.Property.Title}'.",
            NotificationType.General, $"/HostBookings/Details/{id}");

        TempData["Success"] = "Your booking was cancelled.";
        return RedirectToAction(nameof(Trips));
    }

    // ---------- Multi-step check-in ----------

    [HttpGet]
    public async Task<IActionResult> CheckIn(int id)
    {
        var booking = await LoadBookingAsync(id);
        if (booking == null) return NotFound();

        var blocked = CheckInBlockedReason(booking);
        if (blocked != null)
        {
            TempData["Error"] = blocked;
            return RedirectToAction(nameof(Booking), new { id });
        }

        var checkIn = await EnsureCheckInAsync(booking);
        return View(new GuestCheckInViewModel
        {
            Booking = booking,
            CheckIn = checkIn,
            ProcessTitle = checkIn.CheckInProcess!.Title,
            Steps = CheckInStep.FromJson(checkIn.CheckInProcess.StepsJson)
        });
    }

    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(30_000_000)]
    [ActionName("CheckIn")]
    public async Task<IActionResult> SubmitCheckIn(int id)
    {
        var booking = await LoadBookingAsync(id);
        if (booking == null) return NotFound();

        var blocked = CheckInBlockedReason(booking);
        if (blocked == null && booking.GuestCheckIn?.Status == CheckInStatus.Verified)
            blocked = "Your documents are already verified.";
        if (blocked != null)
        {
            TempData["Error"] = blocked;
            return RedirectToAction(nameof(Booking), new { id });
        }

        var checkIn = await EnsureCheckInAsync(booking);
        var steps = CheckInStep.FromJson(checkIn.CheckInProcess!.StepsJson);
        var form = Request.Form;

        var errors = new List<string>();
        var uploads = new List<(CheckInStep Step, IFormFile File)>();

        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            switch (step.StepType)
            {
                case CheckInStepType.Document:
                    var file = form.Files.GetFile($"doc_{i}");
                    var hasValidExisting = checkIn.Documents.Any(d =>
                        d.DocumentType == step.DocumentType && d.Status != DocumentStatus.Rejected);

                    if (file == null || file.Length == 0)
                    {
                        if (!hasValidExisting) errors.Add($"{step.Name}: please upload your {step.DocumentType}.");
                    }
                    else if (!_files.IsValidDocument(file, out var fileError))
                    {
                        errors.Add(fileError);
                    }
                    else
                    {
                        uploads.Add((step, file));
                    }
                    break;

                case CheckInStepType.Agreement:
                    if (form[$"agree_{i}"] != "on") errors.Add($"{step.Name}: please accept to continue.");
                    break;
            }
        }

        if (errors.Any())
        {
            TempData["Error"] = string.Join(" ", errors);
            return RedirectToAction(nameof(CheckIn), new { id });
        }

        // Replace any earlier upload of the same document type
        var replacedFiles = new List<string>();
        foreach (var (step, file) in uploads)
        {
            foreach (var old in checkIn.Documents.Where(d => d.DocumentType == step.DocumentType).ToList())
            {
                replacedFiles.Add(old.FileName);
                _db.GuestDocuments.Remove(old);
            }

            var stored = await _files.SavePrivateAsync(file, "documents");
            checkIn.Documents.Add(new GuestDocument
            {
                DocumentType = step.DocumentType!,
                FileName = stored,
                Status = DocumentStatus.Pending
            });
        }

        checkIn.Status = CheckInStatus.Submitted;
        await _db.SaveChangesAsync();
        foreach (var name in replacedFiles) _files.DeletePrivate("documents", name);

        await _log.LogAsync(UserId, $"CheckInSubmitted: booking #{id}", ActivityType.CheckIn);
        await _notify.NotifyAsync(booking.Property!.HostId, "Check-in Documents Submitted",
            $"Your guest submitted check-in documents for '{booking.Property.Title}'. Please verify them.",
            NotificationType.CheckInUpdate, $"/HostBookings/Details/{id}");

        TempData["Success"] = "Check-in submitted. Your host will verify your documents.";
        return RedirectToAction(nameof(Booking), new { id });
    }

    // ---------- Helpers ----------

    private Task<Booking?> LoadBookingAsync(int id) =>
        _db.Bookings
            .Include(b => b.Property).ThenInclude(p => p!.Images)
            .Include(b => b.Property).ThenInclude(p => p!.Host)
            .Include(b => b.Property).ThenInclude(p => p!.CheckInProcess)
            .Include(b => b.Payment)
            .Include(b => b.GuestCheckIn).ThenInclude(c => c!.Documents)
            .Include(b => b.GuestCheckIn).ThenInclude(c => c!.CheckInProcess)
            .AsSplitQuery()
            .FirstOrDefaultAsync(b => b.Id == id && b.GuestId == UserId);

    private static string? CheckInBlockedReason(Booking booking) => booking.Status switch
    {
        BookingStatus.Pending => "Check-in opens once the host approves your booking.",
        BookingStatus.CheckedIn or BookingStatus.Completed => "You are already checked in.",
        BookingStatus.Approved when booking.Payment?.Status != PaymentStatus.Paid => "Please complete payment before check-in.",
        BookingStatus.Approved => null,
        _ => "Check-in is not available for this booking."
    };

    private async Task<GuestCheckIn> EnsureCheckInAsync(Booking booking)
    {
        if (booking.GuestCheckIn != null) return booking.GuestCheckIn;

        // Hosts who have not configured a process get the default steps
        var process = booking.Property!.CheckInProcess;
        if (process == null)
        {
            process = new CheckInProcess
            {
                PropertyId = booking.PropertyId,
                Title = $"Check-in for {booking.Property.Title}",
                StepsJson = CheckInStep.ToJson(CheckInStep.Defaults())
            };
            _db.CheckInProcesses.Add(process);
        }

        var checkIn = new GuestCheckIn { Booking = booking, CheckInProcess = process, Status = CheckInStatus.InProgress };
        _db.GuestCheckIns.Add(checkIn);
        await _db.SaveChangesAsync();

        await _log.LogAsync(UserId, $"CheckInStarted: booking #{booking.Id}", ActivityType.CheckIn);
        return checkIn;
    }
}
