using Kayane.Data;
using Kayane.Models;
using Kayane.Services;
using Kayane.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Kayane.Areas.Admin.Controllers;

[Area("Admin")]
public class AuthController : Controller
{
    private readonly KayaneDb _context;
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        KayaneDb context,
        IAuthService authService,
        ILogger<AuthController> logger)
    {
        _context = context;
        _authService = authService;
        _logger = logger;
    }

    // GET: /{adminPrefix}/Auth/Login
    [HttpGet]
    [EnableRateLimiting("login")]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true && User.IsInRole("Admin"))
        {
            return RedirectToAction("Index", "Dashboard");
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginVM());
    }

    // POST: /{adminPrefix}/Auth/Login
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginVM model, string? returnUrl = null)
    {
        if (!ModelState.IsValid)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View(model);
        }

        var normalizedEmail = model.Email.Trim().ToLowerInvariant();
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail);

        // Always return the same generic message — do not reveal
        // whether the account exists or is simply not an admin.
        if (user == null || user.Role != UserRole.Admin)
        {
            _logger.LogWarning("Failed admin login attempt for {Email} from {IP}",
                normalizedEmail, HttpContext.Connection.RemoteIpAddress);
            ModelState.AddModelError(string.Empty, "Invalid credentials.");
            ViewData["ReturnUrl"] = returnUrl;
            return View(model);
        }

        var passwordHasher = new PasswordHasher<User>();
        var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.Password);

        if (result != PasswordVerificationResult.Success &&
            result != PasswordVerificationResult.SuccessRehashNeeded)
        {
            _logger.LogWarning("Failed admin login attempt for {Email} from {IP}",
                normalizedEmail, HttpContext.Connection.RemoteIpAddress);
            ModelState.AddModelError(string.Empty, "Invalid credentials.");
            ViewData["ReturnUrl"] = returnUrl;
            return View(model);
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, model.Password);
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
        }

        await _authService.SignInAsync(user, null, model.RememberMe);

        _logger.LogInformation("Admin {Email} logged in successfully.", user.Email);

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToAction("Index", "Dashboard");
    }

    // POST: /{adminPrefix}/Auth/Logout
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _authService.SignOutAsync();
        return RedirectToAction("Index", "Home", new { area = "" });
    }
}