using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.Services;
using Staybnb.ViewModels;

namespace Staybnb.Controllers;

// Guests apply to become Hosts; existing Hosts use the same flow to list more properties
[Authorize(Roles = AppRoles.Guest + "," + AppRoles.Host)]
public class HostApplicationController : Controller
{
    private const int MaxImages = 10;

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IFileStorageService _files;
    private readonly IActivityLogger _log;
    private readonly INotificationService _notify;

    public HostApplicationController(ApplicationDbContext db, UserManager<ApplicationUser> users,
        IFileStorageService files, IActivityLogger log, INotificationService notify)
    {
        _db = db;
        _users = users;
        _files = files;
        _log = log;
        _notify = notify;
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var userId = _users.GetUserId(User)!;
        if (User.IsInRole(AppRoles.Guest) && await HasPendingAsync(userId))
        {
            TempData["Info"] = "You already have an application under review.";
            return RedirectToAction(nameof(Status));
        }

        return View(new HostApplicationViewModel { AvailableAmenities = await LoadAmenitiesAsync() });
    }

    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(60_000_000)]
    public async Task<IActionResult> Create(HostApplicationViewModel model)
    {
        var userId = _users.GetUserId(User)!;
        if (User.IsInRole(AppRoles.Guest) && await HasPendingAsync(userId))
        {
            TempData["Info"] = "You already have an application under review.";
            return RedirectToAction(nameof(Status));
        }

        // An application is incomplete without at least one image
        var images = model.Images?.Where(f => f.Length > 0).ToList() ?? new();
        if (images.Count == 0)
            ModelState.AddModelError(nameof(model.Images), "At least one property image is required.");
        if (images.Count > MaxImages)
            ModelState.AddModelError(nameof(model.Images), $"You can upload up to {MaxImages} images.");
        foreach (var img in images)
            if (!_files.IsValidImage(img, out var error))
                ModelState.AddModelError(nameof(model.Images), error);

        if (!HostApplicationViewModel.PropertyTypes.Contains(model.PropertyType))
            ModelState.AddModelError(nameof(model.PropertyType), "Choose a valid property type.");

        if (!ModelState.IsValid)
        {
            model.AvailableAmenities = await LoadAmenitiesAsync();
            return View(model);
        }

        var amenities = await _db.Amenities.Where(a => model.SelectedAmenityIds.Contains(a.Id)).ToListAsync();

        var property = new HostProperty
        {
            Title = model.Title.Trim(),
            Description = model.Description.Trim(),
            PropertyType = model.PropertyType,
            Address = model.Address.Trim(),
            City = model.City.Trim(),
            MaxGuests = model.MaxGuests,
            Bedrooms = model.Bedrooms,
            Beds = model.Beds,
            Bathrooms = model.Bathrooms,
            PricePerNight = model.PricePerNight,
            CleaningFee = model.CleaningFee,
            HostId = userId,
            IsActive = false,          // goes live only after Admin approval
            Amenities = amenities
        };

        var savedUrls = new List<string>();
        try
        {
            foreach (var img in images)
            {
                var url = await _files.SaveAsync(img, "properties");
                savedUrls.Add(url);
                property.Images.Add(new PropertyImage { ImageUrl = url });
            }

            _db.HostProperties.Add(property);
            _db.HostApplications.Add(new HostApplication { ApplicationUserId = userId, Property = property });
            await _db.SaveChangesAsync();
        }
        catch
        {
            foreach (var url in savedUrls) _files.Delete(url);   // no orphan files if the save fails
            throw;
        }

        var isAdditional = User.IsInRole(AppRoles.Host);
        await _log.LogAsync(userId,
            isAdditional ? "AdditionalPropertyApplicationSubmitted" : "HostApplicationSubmitted",
            ActivityType.HostApplication);

        await _notify.NotifyAsync(userId,
            isAdditional ? "Additional Property Application Submitted" : "Host Application Submitted",
            $"Your application for '{property.Title}' has been received and is under review.",
            NotificationType.General, "/HostApplication/Status");

        foreach (var admin in await _users.GetUsersInRoleAsync(AppRoles.Admin))
            await _notify.NotifyAsync(admin.Id, "New Host Application",
                $"'{property.Title}' in {property.City} needs review.",
                NotificationType.General, "/Admin/Applications");

        TempData["Success"] = "Application submitted. An admin will review it shortly.";
        return RedirectToAction(nameof(Status));
    }

    [HttpGet]
    public async Task<IActionResult> Status()
    {
        var userId = _users.GetUserId(User)!;
        var applications = await _db.HostApplications
            .Include(a => a.Property).ThenInclude(p => p!.Images)
            .Where(a => a.ApplicationUserId == userId)
            .OrderByDescending(a => a.AppliedAt)
            .ToListAsync();

        return View(applications);
    }

    private Task<bool> HasPendingAsync(string userId) =>
        _db.HostApplications.AnyAsync(a => a.ApplicationUserId == userId && a.Status == ApplicationStatus.Pending);

    private Task<List<Amenity>> LoadAmenitiesAsync() =>
        _db.Amenities.OrderBy(a => a.Name).ToListAsync();
}
