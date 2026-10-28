using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.Services;
using Staybnb.ViewModels;

namespace Staybnb.Controllers;

// Public: anyone can browse active listings
public class PropertiesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IBookingService _bookings;

    public PropertiesController(ApplicationDbContext db, UserManager<ApplicationUser> users, IBookingService bookings)
    {
        _db = db;
        _users = users;
        _bookings = bookings;
    }

    public async Task<IActionResult> Index(string? city, string? type, int? guests)
    {
        var query = _db.HostProperties
            .Where(p => p.IsActive)
            .Include(p => p.Images)
            .Include(p => p.Reviews)
            .AsSplitQuery()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(city)) query = query.Where(p => p.City.Contains(city.Trim()));
        if (!string.IsNullOrWhiteSpace(type)) query = query.Where(p => p.PropertyType == type);
        if (guests is > 0) query = query.Where(p => p.MaxGuests >= guests);

        var model = new PropertySearchViewModel
        {
            City = city,
            Type = type,
            Guests = guests,
            Results = await query.OrderByDescending(p => p.CreatedAt).ToListAsync(),
            Cities = await _db.HostProperties.Where(p => p.IsActive)
                .Select(p => p.City).Distinct().OrderBy(c => c).ToListAsync()
        };
        return View(model);
    }

    // URL stays /Properties/View/{id} as in the brief; method is named Details to avoid hiding Controller.View()
    [ActionName("View")]
    public async Task<IActionResult> Details(int id)
    {
        var property = await _db.HostProperties
            .Include(p => p.Images)
            .Include(p => p.Amenities)
            .Include(p => p.Host)
            .Include(p => p.Reviews).ThenInclude(r => r.Reviewer)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id);
        if (property == null) return NotFound();

        // Inactive listings are visible only to their host and admins (preview)
        var userId = _users.GetUserId(User);
        var isOwnerOrAdmin = property.HostId == userId || User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.SuperAdmin);
        if (!property.IsActive && !isOwnerOrAdmin) return NotFound();

        var model = new PropertyDetailsViewModel
        {
            Property = property,
            AverageRating = property.Reviews.Any() ? property.Reviews.Average(r => r.Rating) : 0,
            UnavailableRanges = await _bookings.UpcomingConfirmedAsync(property.Id),
            IsGuest = User.IsInRole(AppRoles.Guest),
            IsSignedIn = User.Identity?.IsAuthenticated == true,
            DefaultCheckIn = DateTime.Today.AddDays(1),
            DefaultCheckOut = DateTime.Today.AddDays(2)
        };
        return View("View", model);
    }
}
