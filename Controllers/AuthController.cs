using Kayane.Data;
using Kayane.Models;
using Kayane.Services;
using Kayane.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Kayane.Controllers
{
    public class AuthController : Controller
    {
        private readonly KayaneDb _context;
        private readonly IAuthService _authService;
        private readonly IEmailService _emailService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            KayaneDb context,
            IAuthService authService,
            IEmailService emailService,
            ILogger<AuthController> logger)
        {
            _context = context;
            _authService = authService;
            _emailService = emailService;
            _logger = logger;
        }

        // ===================== VENDOR REGISTRATION =====================

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
        [EnableRateLimiting("register")]
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

        // ===================== BUYER REGISTRATION =====================

        // GET: /Auth/Register
        [HttpGet]
        public IActionResult Register(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return LocalRedirect(returnUrl ?? "~/");
            }
            ViewData["ReturnUrl"] = returnUrl;
            return View(new RegisterVM());
        }

        // POST: /Auth/RegisterBuyer
        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("register")]
        public async Task<IActionResult> RegisterBuyer(RegisterVM model, string? returnUrl = null)
        {
            returnUrl ??= Url.Action("Index", "Home");

            if (!ModelState.IsValid)
            {
                ViewData["ReturnUrl"] = returnUrl;
                return View("Register", model);
            }

            var normalizedEmail = model.Email.Trim().ToLowerInvariant();

            if (await _context.Users.AnyAsync(u => u.Email == normalizedEmail))
            {
                ModelState.AddModelError("Email", "An account with this email already exists.");
                ViewData["ReturnUrl"] = returnUrl;
                return View("Register", model);
            }

            var user = new User
            {
                UserId = Guid.NewGuid(),
                Name = model.Name.Trim(),
                Email = normalizedEmail,
                Role = UserRole.User,
                CreatedAt = DateTime.UtcNow
            };

            var passwordHasher = new PasswordHasher<User>();
            user.PasswordHash = passwordHasher.HashPassword(user, model.Password);

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation("New buyer registered: {Email}", normalizedEmail);

            await _authService.SignInAsync(user);

            TempData["SuccessMessage"] = "Welcome to Kayane! Your account is ready.";
            return LocalRedirect(returnUrl);
        }

        // ===================== LOGIN / LOGOUT =====================

        [HttpGet]
        [EnableRateLimiting("login")]
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
        [EnableRateLimiting("login")]
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

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) && returnUrl != "/")
            {
                return LocalRedirect(returnUrl);
            }

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

        // ===================== FORGOT PASSWORD =====================

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");

            return View(new ForgotPasswordVM());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("forgot-password")]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordVM model)
        {
            if (!ModelState.IsValid) return View(model);

            var normalizedEmail = model.Email.Trim().ToLowerInvariant();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);

            // Always show the same message, regardless of whether the account exists.
            // Otherwise attackers can enumerate accounts.
            TempData["SuccessMessage"] =
                "If an account with that email exists, a reset link has been sent.";

            if (user == null) return RedirectToAction(nameof(Login));

            // Generate a random token, store only the hash.
            var token = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
                .Replace("+", "-").Replace("/", "_").TrimEnd('=');
            var tokenHash = ComputeSha256Hash(token);

            user.ResetTokenHash = tokenHash;
            user.ResetTokenExpiresAt = DateTime.UtcNow.AddHours(1);
            await _context.SaveChangesAsync();

            var resetUrl = Url.Action(
                "ResetPassword",
                "Auth",
                new { token, email = user.Email },
                Request.Scheme);

            var html = $@"
        <div style='font-family: Arial, sans-serif; padding: 20px; max-width: 560px;'>
            <h2 style='color: #4f46e5;'>Reset Your Kayane Password</h2>
            <p>Hello {System.Net.WebUtility.HtmlEncode(user.Name)},</p>
            <p>We received a request to reset your password. Click the button below to choose a new one.</p>
            <p style='margin: 24px 0;'>
                <a href='{resetUrl}'
                   style='background: #4f46e5; color: white; padding: 12px 24px; text-decoration: none; border-radius: 8px; font-weight: bold;'>
                    Reset Password
                </a>
            </p>
            <p style='font-size: 12px; color: #888;'>This link expires in 1 hour. If you didn't request it, ignore this email.</p>
        </div>";

            try
            {
                await _emailService.SendEmailAsync(user.Email, "Reset Your Kayane Password", html);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Password reset email failed for {Email}", user.Email);
            }

            return RedirectToAction(nameof(Login));
        }

        // ===================== RESET PASSWORD =====================

        [HttpGet]
        public IActionResult ResetPassword(string token, string email)
        {
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(email))
                return RedirectToAction(nameof(Login));

            return View(new ResetPasswordVM { Token = token, Email = email });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordVM model)
        {
            if (!ModelState.IsValid) return View(model);

            var normalizedEmail = model.Email.Trim().ToLowerInvariant();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);

            if (user == null ||
                string.IsNullOrWhiteSpace(user.ResetTokenHash) ||
                user.ResetTokenExpiresAt == null ||
                user.ResetTokenExpiresAt < DateTime.UtcNow)
            {
                TempData["ErrorMessage"] = "Invalid or expired reset link. Please request a new one.";
                return RedirectToAction(nameof(ForgotPassword));
            }

            var tokenHash = ComputeSha256Hash(model.Token);
            if (!string.Equals(user.ResetTokenHash, tokenHash, StringComparison.Ordinal))
            {
                TempData["ErrorMessage"] = "Invalid or expired reset link. Please request a new one.";
                return RedirectToAction(nameof(ForgotPassword));
            }

            var passwordHasher = new Microsoft.AspNetCore.Identity.PasswordHasher<User>();
            user.PasswordHash = passwordHasher.HashPassword(user, model.NewPassword);

            // Invalidate the token — one-time use only.
            user.ResetTokenHash = null;
            user.ResetTokenExpiresAt = null;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Password reset successfully for {Email}", user.Email);

            TempData["SuccessMessage"] = "Your password has been reset. Please sign in.";
            return RedirectToAction(nameof(Login));
        }

        // Helper
        private static string ComputeSha256Hash(string input)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(input);
            var hash = System.Security.Cryptography.SHA256.HashData(bytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}