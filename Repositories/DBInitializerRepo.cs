using Microsoft.AspNetCore.Identity;
using PTO_Management_System.Data;
using PTO_Management_System.Interfaces;
using PTO_Management_System.Models;

namespace PTO_Management_System.Repositories
{
    /// <summary>
    /// Same idea as DBInitializerRepo in the CUT project: create the database tables and seed default data.
    /// Runs automatically when the site starts (see Program.cs) and again from the admin-only Database link.
    /// </summary>
    public class DBInitializerRepo : IDBInitializer
    {
        private readonly PtoDbContext _context;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IConfiguration _configuration;
        private readonly ILogger<DBInitializerRepo> _logger;

        public DBInitializerRepo(PtoDbContext context, RoleManager<IdentityRole> roleManager,
            UserManager<IdentityUser> userManager, IConfiguration configuration, ILogger<DBInitializerRepo> logger)
        {
            _context = context;
            _roleManager = roleManager;
            _userManager = userManager;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            

            // 1. Roles (Tutorial Part 2: "Admin" and "User", plus the PTO Officer from the SRS)
            foreach (var role in new[] { WebConstants.RoleAdmin, WebConstants.RoleOfficer, WebConstants.RoleUser })
            {
                if (!await _roleManager.RoleExistsAsync(role))
                    await _roleManager.CreateAsync(new IdentityRole(role));
            }

            // 2. Default System Administrator (change the password in appsettings / user-secrets)
            string adminEmail = _configuration["SeedAdmin:Email"] ?? "admin@gmail.com";
            string adminPassword = _configuration["SeedAdmin:Password"] ?? "Test!123";
            await EnsureStaffUser(adminEmail, adminPassword, WebConstants.RoleAdmin);

            // 3. A PTO Officer for testing the officer role
           // await EnsureStaffUser("officer@gmail.com", adminPassword, WebConstants.RoleOfficer);

            // 4. Sample sites (limited per area)
            if (!_context.Sites.Any())
            {
                var sites = new List<Site>
                {
                    NewSite("BS-0142", "Botshabelo", 250, "-29.2667, 26.7167"),
                    NewSite("BS-0143", "Botshabelo", 250, "-29.2668, 26.7170"),
                    NewSite("BS-0150", "Botshabelo", 250, "-29.2671, 26.7175"),
                    NewSite("BS-0161", "Botshabelo", 280, "-29.2675, 26.7181"),
                    NewSite("TN-0021", "Thaba Nchu", 300, "-29.2100, 26.8300"),
                    NewSite("TN-0022", "Thaba Nchu", 300, "-29.2102, 26.8304"),
                    NewSite("MZ-0101", "Mangweni Z1", 260, "-29.0850, 26.1600"),
                    NewSite("MZ-0102", "Mangweni Z1", 260, "-29.0852, 26.1604")
                };
                _context.Sites.AddRange(sites);
                await _context.SaveChangesAsync();
            }

            // 5. Areas. Sites are LIMITED per area; the system decides once the application target is reached.
            //    (Botshabelo: 4 sites, decision after 6 applications ...). The administrator can change the targets.
            var targets = new Dictionary<string, int> { { "Botshabelo", 6 }, { "Thaba Nchu", 3 }, { "Mangweni Z1", 3 } };
            var known = _context.Areas.Select(a => a.Name).ToList();
            foreach (var settlement in _context.Sites.Select(s => s.Settlement).Distinct().ToList())
            {
                if (known.Contains(settlement)) continue;
                _context.Areas.Add(new Area { Name = settlement, ApplicationTarget = targets.ContainsKey(settlement) ? targets[settlement] : 5 });
            }
            await _context.SaveChangesAsync();
        }

        private async Task EnsureStaffUser(string email, string password, string role)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
                var result = await _userManager.CreateAsync(user, password);
                if (!result.Succeeded)
                {
                    _logger.LogError("Could not create {Email}: {Errors}", email,
                        string.Join("; ", result.Errors.Select(e => e.Description)));
                    return;
                }
            }
            if (!await _userManager.IsInRoleAsync(user, role))
                await _userManager.AddToRoleAsync(user, role);
        }

        private static Site NewSite(string number, string settlement, int size, string gps)
        {
            return new Site
            {
                SiteNumber = number,
                Settlement = settlement,
                SizeSqm = size,
                Location = gps,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
        }
    }
}
