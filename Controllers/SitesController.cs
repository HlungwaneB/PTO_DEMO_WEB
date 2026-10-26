using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PTO_Management_System.Interfaces;
using PTO_Management_System.Models;

namespace PTO_Management_System.Controllers
{
    /// <summary>Screen 33 (Sites) and 34 (Add site). Officers can look, only the administrator can change.</summary>
    [Authorize(Roles = WebConstants.RoleAdmin)]
    public class SitesController : Controller
    {
        private readonly ISite _sites;
        private readonly IAuditLog _audit;
        private readonly IPtoApplication _applications;

        public SitesController(ISite sites, IAuditLog audit, IPtoApplication applications)
        {
            _sites = sites;
            _audit = audit;
            _applications = applications;
        }

        public IActionResult Index(string filter = "All", string search = null)
        {
            var rows = _sites.Rows();
            if (filter == "Available" || filter == "Occupied") rows = rows.Where(r => r.Status == filter).ToList();
            else filter = "All";

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                rows = rows.Where(r => r.Site.SiteNumber.ToLower().Contains(s)
                                       || r.Site.Settlement.ToLower().Contains(s)).ToList();
            }
            ViewBag.Areas = _sites.Areas();
            ViewBag.Filter = filter;
            ViewBag.Search = search;
            return View(rows);
        }

        /// <summary>Administrator sets how many applications an area must collect before the system decides.</summary>
        [Authorize(Roles = WebConstants.RoleAdmin)]
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult SetTarget(int areaId, int target)
        {
            if (target < 1 || target > 1000)
            {
                TempData["Error"] = "The target must be between 1 and 1000.";
                return RedirectToAction(nameof(Index));
            }
            try
            {
                _sites.SetTarget(areaId, target);
                var area = _sites.Areas().First(a => a.Area.Id == areaId);
                _applications.RunAllocationFor(area.Area.Name); // the area may now be ready for a decision
                _audit.Log("Administrator", "Updated application target to " + target, area.Area.Name);
                TempData["Message"] = "Application target saved.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = WebConstants.RoleAdmin)]
        [HttpGet]
        public IActionResult Create() => View(new Site());

        [Authorize(Roles = WebConstants.RoleAdmin)]
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Create(Site site)
        {
            if (!string.IsNullOrWhiteSpace(site.SiteNumber) && _sites.SiteNumberExists(site.SiteNumber.Trim()))
                ModelState.AddModelError(nameof(site.SiteNumber), "This site number already exists.");
            if (!ModelState.IsValid) return View(site);

            site.SiteNumber = site.SiteNumber.Trim();
            site.Settlement = site.Settlement.Trim();
            _sites.Create(site);
            _applications.RunAllocationFor(site.Settlement); // new free site: waiting applicants may be decided
            _audit.Log("Administrator", "Added site", site.SiteNumber);
            TempData["Message"] = "Site " + site.SiteNumber + " added.";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = WebConstants.RoleAdmin)]
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Remove(int id)
        {
            var site = _sites.GetById(id);
            if (site == null) return NotFound();

            if (!_sites.Remove(id))
                TempData["Error"] = "A site that is linked to a PTO cannot be removed.";
            else
            {
                _audit.Log("Administrator", "Removed site", site.SiteNumber);
                TempData["Message"] = "Site " + site.SiteNumber + " removed.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
