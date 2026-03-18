using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Reports.Data;

var builder = WebApplication.CreateBuilder(args);

// Register text encoding provider (for Excel/PDF encoding compatibility)
System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

// Add MVC support
builder.Services.AddControllersWithViews();

// Register your DbContext
builder.Services.AddDbContext<ReportsDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Log environment for debugging
Console.WriteLine($"ENV = {Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}");

// Configure large file uploads (200 MB)
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 209_715_200; // 200 MB
});

// Add session support (important for UserRole storage)
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);  // session duration
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true; // Required for GDPR compliance
});

// Register IHttpContextAccessor (fixes your view injection issue)
builder.Services.AddHttpContextAccessor();

// Add cookie-based authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/UserAccounts/Login";
        options.LogoutPath = "/UserAccounts/Logout";
        options.AccessDeniedPath = "/UserAccounts/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
    });

var app = builder.Build();

// Configure middleware pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Enable session before authentication
app.UseSession();

// Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// Default route configuration
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=UserAccounts}/{action=Login}/{id?}");

app.Run();
