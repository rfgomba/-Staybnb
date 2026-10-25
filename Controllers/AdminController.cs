using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Staybnb.Models;

namespace Staybnb.Controllers;

[Authorize(Roles = AppRoles.Admin + "," + AppRoles.SuperAdmin)]
public class AdminController : Controller
{
    public IActionResult Dashboard() => View();   // built out in Step 6
}
