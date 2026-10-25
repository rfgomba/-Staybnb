using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.Services;

namespace Staybnb.Controllers;

[Authorize(Roles = AppRoles.Admin + "," + AppRoles.SuperAdmin)]
public class AdminController : Controller
{
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

    public IActionResult Dashboard() => View();   // built out in Step 6

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

        var adminId = _users.GetUserId(User)!;
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
                NotificationType.HostApplicationApproved, "/Host/Dashboard");
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

        var adminId = _users.GetUserId(User)!;
        application.Status = ApplicationStatus.Rejected;
        application.ReviewedAt = DateTime.UtcNow;
        application.AdminComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim()[..Math.Min(comment.Trim().Length, 500)];
        await _db.SaveChangesAsync();

        await _notify.NotifyAsync(application.ApplicationUserId, "Host Application Rejected",
            $"Your application for '{application.Property!.Title}' was not approved. {application.AdminComment}",
            NotificationType.HostApplicationRejected, "/HostApplication/Status");
        await _log.LogAsync(adminId, $"HostApplicationRejected: #{application.Id} {application.Property.Title}", ActivityType.HostApplication);

        TempData["Success"] = $"Rejected '{application.Property.Title}'.";
        return RedirectToAction(nameof(Applications));
    }
}
