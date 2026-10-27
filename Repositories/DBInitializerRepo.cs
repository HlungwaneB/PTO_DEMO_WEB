using PTO_Management_System.Data;
using PTO_Management_System.Interfaces;
using PTO_Management_System.Models;

namespace PTO_Management_System.Repositories
{
    /// <summary>
    /// Seeds the sample sites and areas when those tables are empty (the Supabase setup script already
    /// does this). Logins are NOT created here any more: they live in Supabase Auth, and the administrator
    /// is created in the Supabase dashboard (Authentication > Users) and given the admin role in profiles.
    /// </summary>
    public class DBInitializerRepo : IDBInitializer
    {
        private readonly PtoDbContext _context;

        public DBInitializerRepo(PtoDbContext context)
        {
            _context = context;
        }

        public async Task InitializeAsync()
        {
            // Sample sites (limited per area), only when the table is empty
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

            // Areas. Sites are LIMITED per area; the system decides once the application target is reached.
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
