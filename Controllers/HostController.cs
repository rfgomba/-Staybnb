using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Staybnb.Models;

namespace Staybnb.Controllers;

[Authorize(Roles = AppRoles.Host)]
public class HostController : Controller
{
    public IActionResult Dashboard() => View();   // built out in Step 4
}
