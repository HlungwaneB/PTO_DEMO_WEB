using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PTO_Management_System.Interfaces;
using PTO_Management_System.Models;

namespace PTO_Management_System.Controllers
{
    /// <summary>
    /// Administrator / officer screens (VIEW ONLY): 30 Dashboard, 31 Applications, 32 PTO records, 35 PTO detail.
    /// The system approves and rejects applications by itself; the administrator monitors and manages sites, areas and reports.
    /// </summary>
    [Authorize(Roles = WebConstants.RoleAdmin + "," + WebConstants.RoleOfficer)]
    public class AdminController : Controller
    {
        private const int PageSize = 8;

        private readonly IPtoApplication _applications;
        private readonly IBeneficiary _beneficiaries;
        private readonly ISite _sites;
        private readonly IExpiryService _expiry;

        public AdminController(IPtoApplication applications, IBeneficiary beneficiaries, ISite sites,
            IExpiryService expiry)
        {
            _applications = applications;
            _beneficiaries = beneficiaries;
            _sites = sites;
            _expiry = expiry;
        }

        // ------------------------------------------------------------ Screen 30: Dashboard

        public IActionResult Dashboard()
        {
            _expiry.Refresh(); // make sure Expiring / Expired are up to date

            var all = _applications.GetAll();
            var model = new DashboardViewModel
            {
                Beneficiaries = _beneficiaries.GetAll().Count(),
                Sites = _sites.GetAll().Count(s => s.IsActive),
                Pending = all.Count(a => a.Status == PtoStatus.Pending),
                Active = all.Count(a => a.Status == PtoStatus.Active),
                Expiring = all.Count(a => a.Status == PtoStatus.Expiring),
                Expired = all.Count(a => a.Status == PtoStatus.Expired),
                Renewed = all.Count(a => a.Status == PtoStatus.Renewed),
                Deregistered = all.Count(a => a.Status == PtoStatus.Deregistered),
                // waiting applications and PTOs that are about to expire or have expired
                NeedsAttention = all.Where(a => a.Status == PtoStatus.Pending || a.Status == PtoStatus.Expiring || a.Status == PtoStatus.Expired)
                                    .OrderByDescending(a => a.SubmittedAt).Take(5).ToList()
            };
            model.RenewalRequests = all.SelectMany(a => a.Renewals).Count(r => r.Status == "Approved");
            model.Areas = _sites.Areas();
            return View(model);
        }

        // ------------------------------------------------------------ Screen 31: Applications (pending first)

        public IActionResult Applications(string filter = "All", string search = null, int page = 1)
        {
            var query = _applications.GetAll().Where(a => a.Status != PtoStatus.Draft);

            if (filter == "Pending" || filter == "Rejected")
                query = query.Where(a => a.Status == filter);
            else if (filter == "Approved")
                query = query.Where(a => a.ApprovedAt != null); // approved automatically by the system
            else
                filter = "All";

            query = ApplySearch(query, search);
            ViewBag.Filter = filter;
            ViewBag.Search = search;
            return View(PaginatedList<PtoApplication>.Create(query.OrderByDescending(a => a.SubmittedAt), page, PageSize));
        }

        // ------------------------------------------------------------ Screen 32: PTO records

        public IActionResult Records(string filter = "All", string search = null, int page = 1)
        {
            var valid = new[] { "All", PtoStatus.Active, PtoStatus.Expiring, PtoStatus.Expired, PtoStatus.Renewed, PtoStatus.Deregistered };
            if (!valid.Contains(filter)) filter = "All";

            var query = _applications.GetAll().Where(a => a.Status != PtoStatus.Draft
                                                          && a.Status != PtoStatus.Pending && a.Status != PtoStatus.Rejected);
            if (filter != "All") query = query.Where(a => a.Status == filter);

            query = ApplySearch(query, search);
            ViewBag.Filter = filter;
            ViewBag.Search = search;
            return View(PaginatedList<PtoApplication>.Create(query.OrderBy(a => a.ExpiryDate), page, PageSize));
        }

        private static IQueryable<PtoApplication> ApplySearch(IQueryable<PtoApplication> query, string search)
        {
            if (string.IsNullOrWhiteSpace(search)) return query;
            var s = search.Trim().ToLower();
            return query.Where(a => a.ReferenceNumber.ToLower().Contains(s)
                                    || (a.PtoNumber != null && a.PtoNumber.ToLower().Contains(s))
                                    || a.Beneficiary.Surname.ToLower().Contains(s)
                                    || a.Beneficiary.FirstNames.ToLower().Contains(s));
        }

        // ------------------------------------------------------------ Screen 35: Detail (read only)

        /// <summary>
        /// Staff can look at every application and see how the system decided (score, rank note, history),
        /// but they cannot approve, reject, renew or de-register: the system and the beneficiary do that.
        /// </summary>
        public IActionResult Detail(int id)
        {
            var app = _applications.GetById(id);
            if (app == null || app.Status == PtoStatus.Draft) return NotFound();

            return View(new DetailViewModel
            {
                Application = app,
                Area = _sites.Areas().FirstOrDefault(a => a.Area.Name == app.Settlement),
                PathwayStage = PtoStatus.PathwayStage(app.Status),
                PathwayText = PtoStatus.PathwayText(app.Status)
            });
        }
    }
}
