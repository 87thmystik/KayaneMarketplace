using Kayane.Data;
using Kayane.Models;
using Kayane.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Configure Nigerian Naira as the app's culture
var cultureInfo = new System.Globalization.CultureInfo("en-NG");
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;

// URL prefixes for private areas (from config)
var adminPrefix = (builder.Configuration["Routing:AdminPrefix"] ?? "admin").Trim('/');
var vendorPrefix = (builder.Configuration["Routing:VendorPrefix"] ?? "vendor").Trim('/');

// 1. Database Connection
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<KayaneDb>(options =>
    options.UseNpgsql(connectionString));

// 2. Cookie Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.AccessDeniedPath = "/Auth/AccessDenied";

        // Security hardening
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;

        // Redirect unauthenticated requests for admin URLs to the admin login,
        // and everything else to the public login.
        options.Events.OnRedirectToLogin = context =>
        {
            var path = context.Request.Path.Value ?? "";
            var adminPath = "/" + adminPrefix;

            if (path.StartsWith(adminPath, StringComparison.OrdinalIgnoreCase))
            {
                var returnUrl = Uri.EscapeDataString(
                    context.Request.Path + context.Request.QueryString);
                context.Response.Redirect($"{adminPath}/Auth/Login?returnUrl={returnUrl}");
            }
            else
            {
                context.Response.Redirect(context.RedirectUri);
            }
            return Task.CompletedTask;
        };
    });

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<Kayane.Filters.NoCacheFilter>();
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("VendorOnly", policy => policy.RequireRole("Vendor"));
    options.AddPolicy("ActiveVendor", policy =>
        policy.RequireRole("Vendor").RequireClaim("VendorStatus", "Active"));
});

// Response compression (gzip / brotli)
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        new[] { "application/json", "text/html", "text/css", "text/javascript", "application/javascript" });
});

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

builder.Services.AddRateLimiter(options =>
{
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "text/html";

        await context.HttpContext.Response.WriteAsync(
            "<!DOCTYPE html><html><head><title>Too Many Requests</title>" +
            "<script src='https://cdn.tailwindcss.com'></script></head>" +
            "<body class='min-h-screen flex items-center justify-center bg-gray-50'>" +
            "<div class='max-w-md bg-white p-8 rounded-2xl border border-gray-200 shadow-sm text-center space-y-3'>" +
            "<h1 class='text-3xl font-extrabold text-gray-900'>Slow Down</h1>" +
            "<p class='text-sm text-gray-500'>Too many attempts. Please wait a moment and try again.</p>" +
            "<a href='/Auth/Login' class='inline-block px-5 py-2.5 bg-indigo-600 hover:bg-indigo-700 text-white text-sm font-semibold rounded-xl'>Back to Login</a>" +
            "</div></body></html>",
            token);
    };

    options.AddPolicy("login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    options.AddPolicy("register", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromMinutes(5),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    options.AddPolicy("forgot-password", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromMinutes(10),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));
});

// Health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<KayaneDb>("database");

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<PaymentVerificationService>();
builder.Services.AddScoped<IAdminAuditService, AdminAuditService>();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<PsbService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IEmailVerificationService, EmailVerificationService>();
builder.Services.AddScoped<IImageService, ImageService>();
builder.Services.AddScoped<ITotpService, TotpService>();

var app = builder.Build();

// ===================== MIDDLEWARE PIPELINE =====================

// 1. Exception handling — dev shows stack traces, prod shows friendly page
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// 2. HTTPS redirect
app.UseHttpsRedirection();

// 3. Security headers on every response
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

    await next();
});

// 4. Response compression
app.UseResponseCompression();

// 5. Static files
app.UseStaticFiles();

// 6. Routing + auth
app.UseRouting();
app.UseRateLimiter();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

// Health check endpoint — no auth, no redirect
app.MapHealthChecks("/health").AllowAnonymous();

// Admin area — lives under /{adminPrefix}/...
app.MapControllerRoute(
    name: "admin-area",
    pattern: $"{adminPrefix}/{{controller=Dashboard}}/{{action=Index}}/{{id?}}",
    defaults: new { area = "Admin" },
    constraints: new { area = "Admin" });

// Vendor area — lives under /{vendorPrefix}/...
app.MapControllerRoute(
    name: "vendor-area",
    pattern: $"{vendorPrefix}/{{controller=Vendor}}/{{action=Dashboard}}/{{id?}}",
    defaults: new { area = "Vendor" },
    constraints: new { area = "Vendor" });

// Public routes — unchanged
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// --- AUTOMATIC ADMIN SEEDER ---
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<KayaneDb>();
        var config = services.GetRequiredService<IConfiguration>();

        bool adminExists = await context.Users.AnyAsync(u => u.Role == UserRole.Admin);

        if (!adminExists)
        {
            var seedEmail = config["SeedAdmin:Email"] ?? "admin@kayane.com";
            var seedPassword = config["SeedAdmin:Password"];
            var seedPhone = config["SeedAdmin:Phone"] ?? "";
            var seedName = config["SeedAdmin:Name"] ?? "System Administrator";

            if (string.IsNullOrWhiteSpace(seedPassword))
            {
                var startupLogger = services.GetRequiredService<ILogger<Program>>();
                startupLogger.LogWarning(
                    "SeedAdmin:Password is not configured. Admin user not created.");
            }
            else
            {
                var passwordHasher = new Microsoft.AspNetCore.Identity.PasswordHasher<Kayane.Models.User>();
                var adminUser = new Kayane.Models.User
                {
                    UserId = Guid.NewGuid(),
                    Name = seedName,
                    Email = seedEmail,
                    Phone = seedPhone,
                    Role = Kayane.Models.UserRole.Admin,
                    IsSuperAdmin = true,
                    CreatedAt = DateTime.UtcNow
                };
                adminUser.PasswordHash = passwordHasher.HashPassword(adminUser, seedPassword);

                context.Users.Add(adminUser);
                await context.SaveChangesAsync();
            }
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the admin user.");
    }
}

app.Run();