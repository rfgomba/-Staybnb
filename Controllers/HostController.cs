using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.Services;
using Staybnb.ViewModels;

namespace Staybnb.Controllers;

[Authorize(Roles = AppRoles.Host)]
public class HostController : Controller
{
    private const int MaxImages = 10;

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IFileStorageService _files;
    private readonly IActivityLogger _log;

    public HostController(ApplicationDbContext db, UserManager<ApplicationUser> users,
        IFileStorageService files, IActivityLogger log)
    {
        _db = db;
        _users = users;
        _files = files;
        _log = log;
    }

    private string UserId => _users.GetUserId(User)!;

    // ---------- Dashboard ----------

    public async Task<IActionResult> Dashboard()
    {
        var userId = UserId;
        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var monthEnd = monthStart.AddMonths(1);
        var daysInMonth = DateTime.DaysInMonth(today.Year, today.Month);

        var properties = await _db.HostProperties
            .Where(p => p.HostId == userId)
            .Select(p => new { p.Id, p.IsActive, p.PricePerNight, HasCheckIn = p.CheckInProcess != null })
            .ToListAsync();

        var approvedIds = await _db.HostApplications
            .Where(a => a.ApplicationUserId == userId && a.Status == ApplicationStatus.Approved)
            .Select(a => a.PropertyId)
            .ToListAsync();

        var bookings = await _db.Bookings
            .Where(b => b.Property!.HostId == userId)
            .Include(b => b.Guest)
            .Include(b => b.Property)
            .Include(b => b.Payment)
            .ToListAsync();

        var ratings = await _db.Reviews
            .Where(r => r.Property!.HostId == userId)
            .Select(r => r.Rating)
            .ToListAsync();

        var confirmed = bookings
            .Where(b => b.Status is BookingStatus.Approved or BookingStatus.CheckedIn or BookingStatus.Completed)
            .ToList();
        var paid = bookings.Where(b => b.Payment?.Status == PaymentStatus.Paid).ToList();

        // Host keeps nightly total + cleaning fee; the service fee goes to the platform
        static decimal HostShare(Booking b) => b.TotalPrice - b.ServiceFee;

        var bookedNightsThisMonth = confirmed.Sum(b =>
            Math.Max(0, (Earlier(b.CheckOutDate, monthEnd) - Later(b.CheckInDate, monthStart)).Days));

        var activeCount = properties.Count(p => p.IsActive);

        var model = new HostDashboardViewModel
        {
            HostName = (await _users.GetUserAsync(User))?.FirstName ?? "Host",
            TotalProperties = properties.Count,
            ActiveProperties = activeCount,
            TotalBookings = bookings.Count(b => b.Status is not (BookingStatus.Rejected or BookingStatus.Cancelled)),
            BookingsThisMonth = bookings.Count(b => b.CreatedAt >= monthStart),
            TotalEarnings = paid.Sum(HostShare),
            EarningsThisMonth = paid.Where(b => b.Payment!.CreatedAt >= monthStart).Sum(HostShare),
            AverageRating = ratings.Count == 0 ? 0 : ratings.Average(),
            ReviewCount = ratings.Count,
            PendingRequests = bookings.Count(b => b.Status == BookingStatus.Pending),
            OccupancyRate = activeCount == 0 ? 0 : (double)bookedNightsThisMonth / (activeCount * daysInMonth) * 100,
            AverageNightlyRate = activeCount == 0 ? 0 : properties.Where(p => p.IsActive).Average(p => p.PricePerNight),
            AverageBookingValue = confirmed.Count == 0 ? 0 : confirmed.Average(b => b.TotalPrice),
            MissingCheckInSetup = properties.Count(p => approvedIds.Contains(p.Id) && !p.HasCheckIn),
            RecentBookings = bookings.OrderByDescending(b => b.CreatedAt).Take(5).ToList()
        };

        return View(model);
    }

    // ---------- My Properties ----------

    public async Task<IActionResult> Properties()
    {
        var userId = UserId;

        var properties = await _db.HostProperties
            .Where(p => p.HostId == userId)
            .Include(p => p.Images)
            .Include(p => p.Bookings)
            .Include(p => p.Reviews)
            .Include(p => p.CheckInProcess)
            .AsSplitQuery()
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        // Latest application status per property
        var applications = await _db.HostApplications
            .Where(a => a.ApplicationUserId == userId)
            .ToListAsync();
        var statusByProperty = applications
            .GroupBy(a => a.PropertyId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.AppliedAt).First().Status);

        var model = properties.Select(p => new HostPropertyListItem
        {
            Property = p,
            Status = statusByProperty.TryGetValue(p.Id, out var s) ? s : null
        }).ToList();

        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var property = await _db.HostProperties.FirstOrDefaultAsync(p => p.Id == id && p.HostId == UserId);
        if (property == null) return NotFound();

        var approved = await _db.HostApplications
            .AnyAsync(a => a.PropertyId == id && a.Status == ApplicationStatus.Approved);
        if (!approved)
        {
            TempData["Error"] = "This property can only be activated after an admin approves it.";
            return RedirectToAction(nameof(Properties));
        }

        property.IsActive = !property.IsActive;
        await _db.SaveChangesAsync();

        var state = property.IsActive ? "Activated" : "Deactivated";
        await _log.LogAsync(UserId, $"Property{state}: #{property.Id} {property.Title}", ActivityType.PropertyChange);

        TempData["Success"] = $"'{property.Title}' is now {(property.IsActive ? "active and visible to guests" : "inactive and hidden from guests")}.";
        return RedirectToAction(nameof(Properties));
    }

    // ---------- Edit property ----------

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var p = await _db.HostProperties
            .Include(x => x.Images)
            .Include(x => x.Amenities)
            .FirstOrDefaultAsync(x => x.Id == id && x.HostId == UserId);
        if (p == null) return NotFound();

        var model = new PropertyEditViewModel
        {
            Id = p.Id,
            Title = p.Title,
            Description = p.Description,
            PropertyType = p.PropertyType,
            Address = p.Address,
            City = p.City,
            MaxGuests = p.MaxGuests,
            Bedrooms = p.Bedrooms,
            Beds = p.Beds,
            Bathrooms = p.Bathrooms,
            PricePerNight = p.PricePerNight,
            CleaningFee = p.CleaningFee,
            SelectedAmenityIds = p.Amenities.Select(a => a.Id).ToList(),
            ExistingImages = p.Images.ToList(),
            AvailableAmenities = await LoadAmenitiesAsync()
        };
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(60_000_000)]
    public async Task<IActionResult> Edit(int id, PropertyEditViewModel model)
    {
        var property = await _db.HostProperties
            .Include(x => x.Images)
            .Include(x => x.Amenities)
            .FirstOrDefaultAsync(x => x.Id == id && x.HostId == UserId);
        if (property == null) return NotFound();

        var newImages = model.Images?.Where(f => f.Length > 0).ToList() ?? new();
        var removeIds = model.RemoveImageIds ?? new();
        var remaining = property.Images.Count(i => !removeIds.Contains(i.Id)) + newImages.Count;

        if (remaining == 0)
            ModelState.AddModelError(nameof(model.Images), "A property must keep at least one image.");
        if (remaining > MaxImages)
            ModelState.AddModelError(nameof(model.Images), $"A property can have up to {MaxImages} images.");
        foreach (var img in newImages)
            if (!_files.IsValidImage(img, out var error))
                ModelState.AddModelError(nameof(model.Images), error);
        if (!HostApplicationViewModel.PropertyTypes.Contains(model.PropertyType))
            ModelState.AddModelError(nameof(model.PropertyType), "Choose a valid property type.");

        if (!ModelState.IsValid)
        {
            model.Id = id;
            model.ExistingImages = property.Images.ToList();
            model.AvailableAmenities = await LoadAmenitiesAsync();
            return View(model);
        }

        property.Title = model.Title.Trim();
        property.Description = model.Description.Trim();
        property.PropertyType = model.PropertyType;
        property.Address = model.Address.Trim();
        property.City = model.City.Trim();
        property.MaxGuests = model.MaxGuests;
        property.Bedrooms = model.Bedrooms;
        property.Beds = model.Beds;
        property.Bathrooms = model.Bathrooms;
        property.PricePerNight = model.PricePerNight;
        property.CleaningFee = model.CleaningFee;

        property.Amenities.Clear();
        foreach (var amenity in await _db.Amenities.Where(a => model.SelectedAmenityIds.Contains(a.Id)).ToListAsync())
            property.Amenities.Add(amenity);

        var imagesToRemove = property.Images.Where(i => removeIds.Contains(i.Id)).ToList();
        _db.PropertyImages.RemoveRange(imagesToRemove);

        var savedUrls = new List<string>();
        try
        {
            foreach (var img in newImages)
            {
                var url = await _files.SaveAsync(img, "properties");
                savedUrls.Add(url);
                property.Images.Add(new PropertyImage { ImageUrl = url });
            }
            await _db.SaveChangesAsync();
        }
        catch
        {
            foreach (var url in savedUrls) _files.Delete(url);
            throw;
        }

        // Delete old files only once the database update has succeeded
        foreach (var img in imagesToRemove) _files.Delete(img.ImageUrl);

        await _log.LogAsync(UserId, $"PropertyUpdated: #{property.Id} {property.Title}", ActivityType.PropertyChange);
        TempData["Success"] = $"'{property.Title}' was updated.";
        return RedirectToAction(nameof(Properties));
    }

    // ---------- Check-in process ----------

    [HttpGet]
    public async Task<IActionResult> CheckIn(int id)
    {
        var property = await _db.HostProperties
            .Include(p => p.CheckInProcess)
            .FirstOrDefaultAsync(p => p.Id == id && p.HostId == UserId);
        if (property == null) return NotFound();

        var process = property.CheckInProcess;
        var model = new CheckInSetupViewModel
        {
            PropertyId = property.Id,
            PropertyTitle = property.Title,
            Title = process?.Title ?? $"Check-in for {property.Title}",
            Steps = process == null ? CheckInStep.Defaults() : CheckInStep.FromJson(process.StepsJson)
        };
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckIn(int id, CheckInSetupViewModel model)
    {
        var property = await _db.HostProperties
            .Include(p => p.CheckInProcess)
            .FirstOrDefaultAsync(p => p.Id == id && p.HostId == UserId);
        if (property == null) return NotFound();

        // Ignore blank rows, tidy text, and only keep a document type on document steps
        var steps = (model.Steps ?? new())
            .Where(s => !string.IsNullOrWhiteSpace(s.Name))
            .Select(s => new CheckInStep
            {
                Name = s.Name!.Trim(),
                StepType = s.StepType,
                DocumentType = s.StepType == CheckInStepType.Document ? s.DocumentType?.Trim() : null,
                Description = s.Description?.Trim()
            })
            .ToList();

        if (steps.Count == 0)
            ModelState.AddModelError(string.Empty, "Add at least one step.");
        if (!steps.Any(s => s.StepType == CheckInStepType.Document))
            ModelState.AddModelError(string.Empty, "Include at least one Document step so guests upload an ID or passport for you to verify.");
        if (steps.Any(s => s.StepType == CheckInStepType.Document && string.IsNullOrWhiteSpace(s.DocumentType)))
            ModelState.AddModelError(string.Empty, "Every Document step needs the document type, for example 'ID or Passport'.");

        if (!ModelState.IsValid)
        {
            model.PropertyId = id;
            model.PropertyTitle = property.Title;
            model.Steps = model.Steps ?? new();
            return View(model);
        }

        var json = CheckInStep.ToJson(steps);
        if (property.CheckInProcess == null)
        {
            property.CheckInProcess = new CheckInProcess { Title = model.Title.Trim(), StepsJson = json };
        }
        else
        {
            property.CheckInProcess.Title = model.Title.Trim();
            property.CheckInProcess.StepsJson = json;
        }
        await _db.SaveChangesAsync();

        await _log.LogAsync(UserId, $"CheckInProcessSaved: #{property.Id} {property.Title}", ActivityType.PropertyChange);
        TempData["Success"] = $"Check-in process saved for '{property.Title}' ({steps.Count} steps).";
        return RedirectToAction(nameof(Properties));
    }

    // ---------- Helpers ----------

    private Task<List<Amenity>> LoadAmenitiesAsync() => _db.Amenities.OrderBy(a => a.Name).ToListAsync();

    private static DateTime Earlier(DateTime a, DateTime b) => a < b ? a : b;
    private static DateTime Later(DateTime a, DateTime b) => a > b ? a : b;
}
