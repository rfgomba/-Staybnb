using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Staybnb.Models;

namespace Staybnb.Controllers;

public class HomeController : Controller
{
    // The property gallery is the home page
    public IActionResult Index() => RedirectToAction("Index", "Properties");

    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
