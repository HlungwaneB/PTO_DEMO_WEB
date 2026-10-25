using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using PTO_Management_System;
using PTO_Management_System.Data;
using PTO_Management_System.Interfaces;
using PTO_Management_System.Models;

// Npgsql (PostgreSQL) is strict about DateTime kinds; this switch keeps simple DateTime values working.
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// Decimal numbers (e.g. 3000.50 from <input type="number">) always use a dot, whatever the server language is.
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = System.Globalization.CultureInfo.InvariantCulture;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(options => options.Filters.AddService<CustomExceptionFilter>());
builder.Services.AddSingleton<IEmailSender, PTO_Management_System.Services.EmailSender>(); builder.Services.AddMemoryCache();

// ---------- Database: Supabase PostgreSQL through Entity Framework Core ----------
builder.Services.AddDbContext<PtoDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ---------- Authentication + roles (ASP.NET Core Identity) ----------
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.User.RequireUniqueEmail = true;

    // Password rules (FR1 / 7.1 Security)
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;

    // Lock the account for 10 minutes after 5 wrong passwords
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
    options.Lockout.AllowedForNewUsers = true;
})
.AddEntityFrameworkStores<PtoDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(2);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;

    // Staff pages send you to the administrator log-in, everything else to the beneficiary log-in
    options.Events.OnRedirectToLogin = context =>
    {
        var path = context.Request.Path;
        bool staffArea = path.StartsWithSegments("/Admin") || path.StartsWithSegments("/Sites")
                      || path.StartsWithSegments("/Reports") || path.StartsWithSegments("/Audit");
        var target = staffArea ? "/Account/AdminLogin" : "/Account/Login";
        context.Response.Redirect(target + "?ReturnUrl=" + Uri.EscapeDataString(context.Request.Path + context.Request.QueryString));
        return Task.CompletedTask;
    };
});

// ---------- Session: keeps the application wizard answers before the account exists ----------
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddHttpClient();
builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
builder.Services.AddScoped<CustomExceptionFilter>();

//Register the client
builder.Services.AddScoped(_ => new Supabase.Client(
    builder.Configuration["Supabase:Url"],
    builder.Configuration["Supabase:Key"],
    new Supabase.SupabaseOptions { AutoRefreshToken = true, AutoConnectRealtime = false }));

// ---------- Repositories (Interfaces/ + Repositories/ pattern from the CUT project) ----------
// Instead of one AddScoped line per repository, every class in the Repositories namespace is registered
// against the interface(s) it implements that live in the Interfaces namespace. This means every team
// member can add a repository file without editing Program.cs (no merge conflicts).
foreach (var implementation in typeof(Program).Assembly.GetTypes()
             .Where(t => t.IsClass && !t.IsAbstract && t.Namespace == "PTO_Management_System.Repositories"))
{
    foreach (var contract in implementation.GetInterfaces()
                 .Where(i => i.Namespace == "PTO_Management_System.Interfaces"))
    {
        builder.Services.AddScoped(contract, implementation);
    }
}

// ---------- File storage: Local (default) or Supabase Storage ----------
// Chosen by appsettings "Storage:Provider". The class is looked up by name so that Program.cs still
// compiles on its own before the storage files have been merged from GitHub.
var storageName = string.Equals(builder.Configuration["Storage:Provider"], "Supabase", StringComparison.OrdinalIgnoreCase)
    ? "SupabaseFileStorage" : "LocalFileStorage";
var storageType = typeof(Program).Assembly.GetType("PTO_Management_System.Storage." + storageName);
if (storageType != null)
    builder.Services.AddScoped(typeof(IFileStorage), storageType);

// Background workers (e.g. ExpiryWorker - the daily check for PTOs that are about to expire, FR8/FR9)
foreach (var worker in typeof(Program).Assembly.GetTypes()
             .Where(t => t.IsClass && !t.IsAbstract && typeof(IHostedService).IsAssignableFrom(t)))
{
    builder.Services.AddSingleton(typeof(IHostedService), worker);
}

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// ---------- Create tables + seed roles, administrator and sites on start-up ----------
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var initializer = scope.ServiceProvider.GetRequiredService<IDBInitializer>();
        await initializer.InitializeAsync();
    }
    catch (Exception ex)
    {
        // The site still starts so you can read the message; fix the connection string and restart.
        logger.LogError(ex, "Database could not be initialised. Check ConnectionStrings:DefaultConnection.");
    }
}

app.Run();
