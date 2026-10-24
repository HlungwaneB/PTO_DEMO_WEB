using PTO_Management_System.Data;
using PTO_Management_System.Interfaces;
using PTO_Management_System.Models;
using Microsoft.EntityFrameworkCore;

namespace PTO_Management_System.Repositories
{
    public class SiteRepo : ISite
    {
        private readonly PtoDbContext _context;

        public SiteRepo(PtoDbContext context)
        {
            _context = context;
        }

        public IQueryable<Site> GetAll()
        {
            return _context.Sites.Where(s => s.IsActive);
        }

        public Site GetById(int id)
        {
            return _context.Sites.FirstOrDefault(s => s.Id == id);
        }

        public bool SiteNumberExists(string siteNumber)
        {
            var number = (siteNumber ?? "").Trim().ToUpper();
            return _context.Sites.Any(s => s.SiteNumber.ToUpper() == number);
        }

        public Site Create(Site site)
        {
            site.SiteNumber = site.SiteNumber.Trim().ToUpper();
            site.Settlement = site.Settlement.Trim();

            // Re-use an existing area with the same name (ignoring upper/lower case)
            var existing = _context.Areas.FirstOrDefault(a => a.Name.ToLower() == site.Settlement.ToLower());
            if (existing != null) site.Settlement = existing.Name;
            site.CreatedAt = DateTime.UtcNow;
            site.IsActive = true;
            _context.Sites.Add(site);

            // A new settlement automatically gets an Area with a default application target
            if (existing == null)
                _context.Areas.Add(new Area { Name = site.Settlement, ApplicationTarget = 5 });

            _context.SaveChanges();
            return site;
        }

        public bool Remove(int id)
        {
            var site = _context.Sites.FirstOrDefault(s => s.Id == id);
            if (site == null) return false;

            var live = PtoStatus.OccupiesSite;
            bool occupied = _context.PtoApplications.Any(a => a.SiteId == id && live.Contains(a.Status));
            if (occupied) return false;

            site.IsActive = false; // soft delete: history that points to this site stays readable
            _context.SaveChanges();
            return true;
        }

        public List<SiteRow> Rows()
        {
            var live = PtoStatus.OccupiesSite;
            var sites = _context.Sites.Where(s => s.IsActive).OrderBy(s => s.SiteNumber).ToList();
            var occupants = _context.PtoApplications
                .Include(a => a.Beneficiary)
                .Where(a => a.SiteId != null && live.Contains(a.Status))
                .ToList();

            var rows = new List<SiteRow>();
            foreach (var site in sites)
            {
                var occupant = occupants.FirstOrDefault(a => a.SiteId == site.Id);
                rows.Add(new SiteRow
                {
                    Site = site,
                    Status = occupant == null ? "Available" : "Occupied",
                    LinkedBeneficiary = occupant?.Beneficiary?.FullName
                });
            }
            return rows;
        }

        public List<AreaRow> Areas()
        {
            var live = PtoStatus.OccupiesSite;
            var takenIds = _context.PtoApplications
                .Where(a => a.SiteId != null && live.Contains(a.Status)).Select(a => a.SiteId.Value).ToList();
            var activeSites = _context.Sites.Where(s => s.IsActive).ToList();
            var pending = _context.PtoApplications.Where(a => a.Status == PtoStatus.Pending)
                .Select(a => a.Settlement).ToList();

            var rows = new List<AreaRow>();
            foreach (var area in _context.Areas.OrderBy(a => a.Name).ToList())
            {
                var sites = activeSites.Where(s => s.Settlement == area.Name).ToList();
                rows.Add(new AreaRow
                {
                    Area = area,
                    TotalSites = sites.Count,
                    FreeSites = sites.Count(s => !takenIds.Contains(s.Id)),
                    Pending = pending.Count(p => p == area.Name)
                });
            }
            return rows;
        }

        public void SetTarget(int areaId, int target)
        {
            var area = _context.Areas.FirstOrDefault(a => a.Id == areaId);
            if (area == null) throw new InvalidOperationException("Area not found.");
            area.ApplicationTarget = target;
            _context.SaveChanges();
        }

        public List<Site> Available()
        {
            return Rows().Where(r => r.Status == "Available").Select(r => r.Site).ToList();
        }
    }
}
