using Kayane.Data;
using Kayane.Models;
using Kayane.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Database Connection
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<KayaneDb>(options =>
    options.UseNpgsql(connectionString));

// 2. Cookie Authentication Configuration
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.AccessDeniedPath = "/Auth/AccessDenied";
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

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<PaymentVerificationService>();
builder.Services.AddScoped<PaymentVerificationService>();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<PsbService>();

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

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
