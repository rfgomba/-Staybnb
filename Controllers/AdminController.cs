using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.Services;
using Staybnb.ViewModels;

namespace Staybnb.Controllers;

[Authorize(Roles = AppRoles.Admin + "," + AppRoles.SuperAdmin)]
public class AdminController : Controller
{
    private const int LogPageSize = 25;
    private static readonly string[] BookingTabs = { "All", "Pending", "Approved", "CheckedIn", "Completed", "Rejected", "Cancelled" };

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IRoleService _roles;
    private readonly IActivityLogger _log;
    private readonly INotificationService _notify;

    public AdminController(ApplicationDbContext db, UserManager<ApplicationUser> users,
        IRoleService roles, IActivityLogger log, INotificationService notify)
    {
        _db = db;
        _users = users;
        _roles = roles;
        _log = log;
        _notify = notify;
    }

    private string UserId => _users.GetUserId(User)!;

    // ---------- Dashboard ----------

    public async Task<IActionResult> Dashboard()
    {
        var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).ToUniversalTime();
        var paid = _db.Bookings.Where(b => b.Payment != null && b.Payment.Status == PaymentStatus.Paid);

        var model = new AdminDashboardViewModel
        {
            TotalUsers = await _db.Users.CountAsync(),
            NewUsersThisMonth = await _db.Users.CountAsync(u => u.CreatedAt >= monthStart),
            PendingApplications = await _db.HostApplications.CountAsync(a => a.Status == ApplicationStatus.Pending),
            ActiveProperties = await _db.HostProperties.CountAsync(p => p.IsActive),
            TotalProperties = await _db.HostProperties.CountAsync(),
            PlatformRevenue = await paid.SumAsync(b => (decimal?)b.ServiceFee) ?? 0,
            GrossBookingValue = await paid.SumAsync(b => (decimal?)b.TotalPrice) ?? 0,
            TotalBookings = await _db.Bookings.CountAsync(),
            PendingBookings = await _db.Bookings.CountAsync(b => b.Status == BookingStatus.Pending),
            ApprovedHosts = (await _users.GetUsersInRoleAsync(AppRoles.Host)).Count,
            RecentActivity = await _db.ActivityLogs.Include(a => a.User)
                .OrderByDescending(a => a.CreatedAt).Take(8).ToListAsync(),
            RecentApplications = await _db.HostApplications.Include(a => a.ApplicationUser).Include(a => a.Property)
                .OrderByDescending(a => a.AppliedAt).Take(5).ToListAsync(),
            RecentBookings = await _db.Bookings.Include(b => b.Guest).Include(b => b.Property).Include(b => b.Payment)
                .OrderByDescending(b => b.CreatedAt).Take(5).ToListAsync()
        };
        return View(model);
    }

    // ---------- Users (SuperAdmin promotes Guests to Admin) ----------

    public async Task<IActionResult> Users(string? search, string? role)
    {
        var query = _db.Users.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(u => u.Email!.Contains(s) || u.FirstName.Contains(s) || u.LastName.Contains(s));
        }
        var users = await query.OrderByDescending(u => u.CreatedAt).ToListAsync();

        var roleMap = (await (from ur in _db.UserRoles
                              join r in _db.Roles on ur.RoleId equals r.Id
                              select new { ur.UserId, r.Name }).ToListAsync())
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Name!).ToList());

        var bookingCounts = await _db.Bookings.GroupBy(b => b.GuestId)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
        var propertyCounts = await _db.HostProperties.GroupBy(p => p.HostId)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);

        var rows = users.Select(u => new AdminUserRow
        {
            User = u,
            Roles = roleMap.GetValueOrDefault(u.Id) ?? new(),
            IsLocked = u.LockoutEnd > DateTimeOffset.UtcNow,
            Bookings = bookingCounts.GetValueOrDefault(u.Id),
            Properties = propertyCounts.GetValueOrDefault(u.Id)
        }).ToList();

        if (!string.IsNullOrWhiteSpace(role))
            rows = rows.Where(r => r.Roles.Contains(role)).ToList();

        return View(new AdminUsersViewModel
        {
            Search = search,
            Role = role,
            IsSuperAdmin = User.IsInRole(AppRoles.SuperAdmin),
            CurrentUserId = UserId,
            Users = rows
        });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> PromoteToAdmin(string id)
    {
        var user = await _users.FindByIdAsync(id);
        if (user == null) return NotFound();

        if (!await _users.IsInRoleAsync(user, AppRoles.Guest))
        {
            TempData["Error"] = "Only Guests can be promoted to Admin.";
            return RedirectToAction(nameof(Users));
        }

        // Adds Admin, removes Guest, logs the role change and notifies the user
        var result = await _roles.PromoteToAdminAsync(user, UserId);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded
            ? $"{user.FullName} is now an Admin."
            : "Promotion failed: " + string.Join("; ", result.Errors.Select(e => e.Description));

        return RedirectToAction(nameof(Users));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleLock(string id)
    {
        var user = await _users.FindByIdAsync(id);
        if (user == null) return NotFound();

        if (user.Id == UserId)
        {
            TempData["Error"] = "You cannot lock your own account.";
            return RedirectToAction(nameof(Users));
        }
        if (await _users.IsInRoleAsync(user, AppRoles.Admin) || await _users.IsInRoleAsync(user, AppRoles.SuperAdmin))
        {
            TempData["Error"] = "Admin accounts cannot be locked.";
            return RedirectToAction(nameof(Users));
        }

        var wasLocked = user.LockoutEnd > DateTimeOffset.UtcNow;
        if (wasLocked)
        {
            await _users.SetLockoutEndDateAsync(user, null);
            await _users.ResetAccessFailedCountAsync(user);
        }
        else
        {
            await _users.SetLockoutEnabledAsync(user, true);
            await _users.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
            await _users.UpdateSecurityStampAsync(user);   // signs the user out on their next request
        }

        await _log.LogAsync(UserId, $"Account{(wasLocked ? "Unlocked" : "Locked")}: {user.Email}", ActivityType.Security);
        TempData["Success"] = $"{user.FullName} has been {(wasLocked ? "unlocked" : "locked")}.";
        return RedirectToAction(nameof(Users));
    }

    // ---------- Bookings and properties ----------

    public async Task<IActionResult> Bookings(string status = "All")
    {
        if (!BookingTabs.Contains(status)) status = "All";

        var all = await _db.Bookings
            .Include(b => b.Guest)
            .Include(b => b.Property).ThenInclude(p => p!.Host)
            .Include(b => b.Payment)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();

        return View(new AdminBookingsViewModel
        {
            Status = status,
            Counts = BookingTabs.ToDictionary(t => t, t => t == "All" ? all.Count : all.Count(b => b.Status.ToString() == t)),
            Bookings = status == "All" ? all : all.Where(b => b.Status.ToString() == status).ToList()
        });
    }

    public async Task<IActionResult> Properties()
    {
        var properties = await _db.HostProperties
            .Include(p => p.Host)
            .Include(p => p.Images)
            .Include(p => p.Bookings)
            .AsSplitQuery()
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        var statuses = (await _db.HostApplications.ToListAsync())
            .GroupBy(a => a.PropertyId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.AppliedAt).First().Status);

        return View(properties.Select(p => new AdminPropertyRow
        {
            Property = p,
            Status = statuses.TryGetValue(p.Id, out var s) ? s : null
        }).ToList());
    }

    // Admins can take a listing offline (quality control) or restore it
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleProperty(int id)
    {
        var property = await _db.HostProperties.FirstOrDefaultAsync(p => p.Id == id);
        if (property == null) return NotFound();

        var approved = await _db.HostApplications.AnyAsync(a => a.PropertyId == id && a.Status == ApplicationStatus.Approved);
        if (!approved)
        {
            TempData["Error"] = "Only approved listings can be activated. Review the application first.";
            return RedirectToAction(nameof(Properties));
        }

        property.IsActive = !property.IsActive;
        await _db.SaveChangesAsync();

        var state = property.IsActive ? "Activated" : "Deactivated";
        await _log.LogAsync(UserId, $"AdminProperty{state}: #{property.Id} {property.Title}", ActivityType.PropertyChange);
        await _notify.NotifyAsync(property.HostId, $"Listing {state}",
            $"An administrator {state.ToLower()} '{property.Title}'.", NotificationType.General, "/Host/Properties");

        TempData["Success"] = $"'{property.Title}' {state.ToLower()}.";
        return RedirectToAction(nameof(Properties));
    }

    // ---------- System audit ----------

    public async Task<IActionResult> ActivityLogs(string? type, string? search, DateTime? from, DateTime? to, int page = 1)
    {
        var query = _db.ActivityLogs.Include(a => a.User).AsQueryable();

        if (Enum.TryParse<ActivityType>(type, out var parsedType))
            query = query.Where(a => a.ActivityType == parsedType);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(a => a.Action.Contains(s) ||
                (a.User != null && (a.User.Email!.Contains(s) || a.User.FirstName.Contains(s) || a.User.LastName.Contains(s))));
        }

        // Logs are stored in UTC; filters are picked as local dates
        if (from.HasValue) query = query.Where(a => a.CreatedAt >= from.Value.Date.ToUniversalTime());
        if (to.HasValue) query = query.Where(a => a.CreatedAt < to.Value.Date.AddDays(1).ToUniversalTime());

        var total = await query.CountAsync();
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)LogPageSize));
        page = Math.Clamp(page, 1, pages);

        return View(new ActivityLogFilterViewModel
        {
            Type = type,
            Search = search,
            From = from,
            To = to,
            Page = page,
            TotalPages = pages,
            TotalCount = total,
            Logs = await query.OrderByDescending(a => a.CreatedAt)
                .Skip((page - 1) * LogPageSize).Take(LogPageSize).ToListAsync()
        });
    }

    // ---------- Host / property applications ----------

    public async Task<IActionResult> Applications(string status = "Pending")
    {
        var query = _db.HostApplications
            .Include(a => a.ApplicationUser)
            .Include(a => a.Property)
            .AsQueryable();

        if (status != "All" && Enum.TryParse<ApplicationStatus>(status, out var parsed))
            query = query.Where(a => a.Status == parsed);

        ViewBag.Status = status;
        return View(await query.OrderByDescending(a => a.AppliedAt).ToListAsync());
    }

    public async Task<IActionResult> ApplicationDetails(int id)
    {
        var application = await _db.HostApplications
            .Include(a => a.ApplicationUser)
            .Include(a => a.Property).ThenInclude(p => p!.Images)
            .Include(a => a.Property).ThenInclude(p => p!.Amenities)
            .FirstOrDefaultAsync(a => a.Id == id);

        return application == null ? NotFound() : View(application);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id)
    {
        var application = await _db.HostApplications
            .Include(a => a.ApplicationUser)
            .Include(a => a.Property)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (application == null) return NotFound();
        if (application.Status != ApplicationStatus.Pending)
        {
            TempData["Error"] = "This application has already been reviewed.";
            return RedirectToAction(nameof(ApplicationDetails), new { id });
        }

        var adminId = UserId;
        var applicant = application.ApplicationUser!;
        var property = application.Property!;

        // Approval, property activation and role swap succeed or fail together
        await using var tx = await _db.Database.BeginTransactionAsync();

        application.Status = ApplicationStatus.Approved;
        application.ReviewedAt = DateTime.UtcNow;
        property.IsActive = true;
        await _db.SaveChangesAsync();

        if (await _users.IsInRoleAsync(applicant, AppRoles.Guest))
        {
            // First approval: Guest becomes Host (adds Host, removes Guest, logs, notifies)
            var result = await _roles.PromoteToHostAsync(applicant, adminId);
            if (!result.Succeeded)
            {
                await tx.RollbackAsync();
                TempData["Error"] = "Role change failed: " + string.Join("; ", result.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(ApplicationDetails), new { id });
            }
        }
        else
        {
            // Existing Host listing an additional property
            await _notify.NotifyAsync(applicant.Id, "Property Approved",
                $"'{property.Title}' is now live on Staybnb.",
                NotificationType.HostApplicationApproved, "/Host/Properties");
        }

        await _log.LogAsync(adminId, $"HostApplicationApproved: #{application.Id} {property.Title}", ActivityType.HostApplication);
        await tx.CommitAsync();

        TempData["Success"] = $"Approved '{property.Title}'. {applicant.FullName} is now a Host.";
        return RedirectToAction(nameof(Applications));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string? comment)
    {
        var application = await _db.HostApplications
            .Include(a => a.Property)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (application == null) return NotFound();
        if (application.Status != ApplicationStatus.Pending)
        {
            TempData["Error"] = "This application has already been reviewed.";
            return RedirectToAction(nameof(ApplicationDetails), new { id });
        }

        var reason = comment?.Trim();
        application.Status = ApplicationStatus.Rejected;
        application.ReviewedAt = DateTime.UtcNow;
        application.AdminComment = string.IsNullOrEmpty(reason) ? null : reason[..Math.Min(reason.Length, 500)];
        await _db.SaveChangesAsync();

        await _notify.NotifyAsync(application.ApplicationUserId, "Host Application Rejected",
            $"Your application for '{application.Property!.Title}' was not approved. {application.AdminComment}",
            NotificationType.HostApplicationRejected, "/HostApplication/Status");
        await _log.LogAsync(UserId, $"HostApplicationRejected: #{application.Id} {application.Property.Title}", ActivityType.HostApplication);

        TempData["Success"] = $"Rejected '{application.Property.Title}'.";
        return RedirectToAction(nameof(Applications));
    }
}
