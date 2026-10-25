using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Staybnb.Models;

namespace Staybnb.Controllers;

[Authorize(Roles = AppRoles.Guest)]
public class GuestController : Controller
{
    public IActionResult Trips() => View();   // built out in Step 5
}
