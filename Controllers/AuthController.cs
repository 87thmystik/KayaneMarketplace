using Kayane.Data;
using Kayane.Models;
using Kayane.Services;
using Kayane.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Kayane.Controllers
{
    public class AuthController : Controller
    {
        private readonly KayaneDb _context;
        private readonly IAuthService _authService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(KayaneDb context, IAuthService authService, ILogger<AuthController> logger)
        {
            _context = context;
            _authService = authService;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult RegisterVendor(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return LocalRedirect(returnUrl ?? Url.Action("Dashboard", "Vendor") ?? "~/");
            }
            ViewData["ReturnUrl"] = returnUrl;
            return View(new RegisterVendorVM());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterVendor(RegisterVendorVM model, string? returnUrl = null)
        {
            returnUrl ??= Url.Action("Dashboard", "Vendor");

            if (!ModelState.IsValid)
            {
                ViewData["ReturnUrl"] = returnUrl;
                return View(model);
            }

            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
            if (existingUser != null)
            {
                ModelState.AddModelError("Email", "An account with this email already exists.");
                ViewData["ReturnUrl"] = returnUrl;
                return View(model);
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var userId = Guid.NewGuid();
                var user = new User
                {
                    UserId = userId,
                    Name = model.FullName,
                    Email = model.Email,
                    Phone = model.Phone,
                    Role = UserRole.Vendor,
                    CreatedAt = DateTime.UtcNow
                };

                var passwordHasher = new PasswordHasher<User>();
                user.PasswordHash = passwordHasher.HashPassword(user, model.Password);

                var vendor = new Vendor
                {
                    VendorId = Guid.NewGuid(),
                    UserId = userId,
                    BusinessName = model.BusinessName,
                    BusinessDescription = model.BusinessDescription,
                    BusinessAddress = model.BusinessAddress,
                    AccountNumber = model.AccountNumber,
                    Status = VendorStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Users.Add(user);
                _context.Vendors.Add(vendor);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                await _authService.SignInAsync(user, vendor);

                TempData["SuccessMessage"] = "Vendor registration submitted! Your account is pending admin approval.";
                return LocalRedirect(returnUrl);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error occurred during vendor registration for {Email}", model.Email);
                ModelState.AddModelError(string.Empty, "An unexpected error occurred. Please try again.");
                ViewData["ReturnUrl"] = returnUrl;
                return View(model);
            }
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return LocalRedirect(returnUrl ?? "~/");
            }
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginVM model, string? returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            if (!ModelState.IsValid)
            {
                ViewData["ReturnUrl"] = returnUrl;
                return View(model);
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                ViewData["ReturnUrl"] = returnUrl;
                return View(model);
            }

            var passwordHasher = new PasswordHasher<User>();
            bool isPasswordValid = false;

            var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.Password);
            if (result == PasswordVerificationResult.Success ||
                result == PasswordVerificationResult.SuccessRehashNeeded)
            {
                isPasswordValid = true;

                if (result == PasswordVerificationResult.SuccessRehashNeeded)
                {
                    user.PasswordHash = passwordHasher.HashPassword(user, model.Password);
                    _context.Users.Update(user);
                    await _context.SaveChangesAsync();
                }
            }

            if (!isPasswordValid)
            {
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                ViewData["ReturnUrl"] = returnUrl;
                return View(model);
            }

            Vendor? vendor = null;
            if (user.Role == UserRole.Vendor)
            {
                vendor = await _context.Vendors.FirstOrDefaultAsync(v => v.UserId == user.UserId);
            }

            await _authService.SignInAsync(user, vendor, model.RememberMe);

            _logger.LogInformation("User {Email} logged in successfully.", user.Email);

            // If a specific return URL was requested and is local, respect it first
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) && returnUrl != "/")
            {
                return LocalRedirect(returnUrl);
            }

            // Otherwise, route based on user role
            if (user.Role == UserRole.Admin)
            {
                return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
            }

            if (user.Role == UserRole.Vendor)
            {
                return RedirectToAction("Dashboard", "Vendor");
            }

            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _authService.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }
    }
}