using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.Services;

namespace Staybnb.Controllers;

/// <summary>Serves guest ID documents only to the guest, the property's host, or an admin.</summary>
[Authorize]
public class DocumentsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IFileStorageService _files;
    private readonly IActivityLogger _log;

    public DocumentsController(ApplicationDbContext db, UserManager<ApplicationUser> users,
        IFileStorageService files, IActivityLogger log)
    {
        _db = db;
        _users = users;
        _files = files;
        _log = log;
    }

    public async Task<IActionResult> Open(int id)
    {
        var doc = await _db.GuestDocuments
            .Include(d => d.GuestCheckIn).ThenInclude(c => c!.Booking).ThenInclude(b => b!.Property)
            .FirstOrDefaultAsync(d => d.Id == id);
        if (doc == null) return NotFound();

        var userId = _users.GetUserId(User);
        var booking = doc.GuestCheckIn!.Booking!;
        var allowed = booking.GuestId == userId
                      || booking.Property!.HostId == userId
                      || User.IsInRole(AppRoles.Admin)
                      || User.IsInRole(AppRoles.SuperAdmin);

        if (!allowed)
        {
            await _log.LogAsync(userId, $"DocumentAccessDenied: document #{id}", ActivityType.Security);
            return Forbid();
        }

        var path = _files.GetPrivatePath("documents", doc.FileName);
        if (!System.IO.File.Exists(path)) return NotFound();

        new FileExtensionContentTypeProvider().TryGetContentType(path, out var contentType);
        return PhysicalFile(path, contentType ?? "application/octet-stream");
    }
}
