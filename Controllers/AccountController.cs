using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Staybnb.Models;
using Staybnb.Services;
using Staybnb.ViewModels;

namespace Staybnb.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly IActivityLogger _log;

    public AccountController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn, IActivityLogger log)
    {
        _users = users;
        _signIn = signIn;
        _log = log;
    }

    [HttpGet, AllowAnonymous]
    public IActionResult Register() =>
        _signIn.IsSignedIn(User) ? RedirectToAction("Index", "Home") : View();

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FirstName = model.FirstName.Trim(),
            LastName = model.LastName.Trim()
        };

        var result = await _users.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, e.Description);
            return View(model);
        }

        // Every new registrant starts as a Guest
        await _users.AddToRoleAsync(user, AppRoles.Guest);
        await _log.LogAsync(user.Id, "UserRegistered", ActivityType.Registration);

        await _signIn.SignInAsync(user, isPersistent: false);
        await _log.LogAsync(user.Id, "UserLoggedIn", ActivityType.Login);

        return RedirectToAction("Index", "Home");
    }

    [HttpGet, AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (_signIn.IsSignedIn(User)) return RedirectToAction("Index", "Home");
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _users.FindByEmailAsync(model.Email);
        if (user == null)
        {
            await _log.LogAsync(null, $"FailedLogin: unknown email {model.Email}", ActivityType.Security);
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

        var result = await _signIn.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            await _log.LogAsync(user.Id, "UserLoggedIn", ActivityType.Login);
            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                return LocalRedirect(model.ReturnUrl);
            return await RedirectByRoleAsync(user);
        }

        if (result.IsLockedOut)
        {
            await _log.LogAsync(user.Id, "AccountLockedOut", ActivityType.Security);
            ModelState.AddModelError(string.Empty, "Account locked after too many attempts. Try again in 5 minutes.");
            return View(model);
        }

        await _log.LogAsync(user.Id, "FailedLoginAttempt", ActivityType.Security);
        ModelState.AddModelError(string.Empty, "Invalid email or password.");
        return View(model);
    }

    [HttpPost, Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var userId = _users.GetUserId(User);
        await _signIn.SignOutAsync();
        await _log.LogAsync(userId, "UserLoggedOut", ActivityType.Logout);
        return RedirectToAction("Index", "Home");
    }

    [HttpGet, AllowAnonymous]
    public async Task<IActionResult> AccessDenied()
    {
        await _log.LogAsync(_users.GetUserId(User), $"AccessDenied: {Request.Query["ReturnUrl"]}", ActivityType.Security);
        return View();
    }

    private async Task<IActionResult> RedirectByRoleAsync(ApplicationUser user)
    {
        var roles = await _users.GetRolesAsync(user);
        if (roles.Contains(AppRoles.SuperAdmin) || roles.Contains(AppRoles.Admin))
            return RedirectToAction("Dashboard", "Admin");
        if (roles.Contains(AppRoles.Host))
            return RedirectToAction("Dashboard", "Host");
        return RedirectToAction("Index", "Home");
    }
}
