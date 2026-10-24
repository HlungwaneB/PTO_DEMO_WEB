using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PTO_Management_System.Interfaces;
using PTO_Management_System.Models;
using PTO_Management_System.Repositories;

namespace PTO_Management_System.Controllers
{
    /// <summary>Screens 36, 37 and 38: report list, generate a report, view/print/download it (FR13).</summary>
    [Authorize(Roles = WebConstants.RoleAdmin + "," + WebConstants.RoleOfficer)]
    public class ReportsController : Controller
    {
        private readonly IReport _reports;
        private readonly IPtoApplication _applications;

        public ReportsController(IReport reports, IPtoApplication applications)
        {
            _reports = reports;
            _applications = applications;
        }

        public IActionResult Index(int page = 1)
        {
            // The four tiles at the top of the Reports screen
            var all = _applications.GetAll();
            ViewBag.Active = all.Count(a => a.Status == PtoStatus.Active);
            ViewBag.Expiring = all.Count(a => a.Status == PtoStatus.Expiring);
            ViewBag.Expired = all.Count(a => a.Status == PtoStatus.Expired);
            ViewBag.Renewed = all.Count(a => a.Status == PtoStatus.Renewed);
            return View(PaginatedList<Report>.Create(_reports.GetAll(), page, 8));
        }

        [HttpGet]
        public IActionResult Generate()
        {
            ViewBag.Types = ReportRepo.ReportTypes;
            return View(new GenerateReportViewModel { Period = DateTime.UtcNow.ToString("MMM yyyy") });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Generate(GenerateReportViewModel model)
        {
            ViewBag.Types = ReportRepo.ReportTypes;
            if (!ReportRepo.ReportTypes.Contains(model.ReportType))
                ModelState.AddModelError(nameof(model.ReportType), "Choose a report from the list.");
            if (!ModelState.IsValid) return View(model);

            var report = _reports.Generate(model.ReportType, model.Period.Trim(), User.Identity.Name);
            return RedirectToAction(nameof(Details), new { id = report.Id });
        }

        /// <summary>Screen 38: shows the CSV as a table and lets the user print it.</summary>
        public IActionResult Details(int id)
        {
            var report = _reports.GetById(id);
            if (report == null) return NotFound();
            return View(report);
        }

        public IActionResult Download(int id)
        {
            var report = _reports.GetById(id);
            if (report == null) return NotFound();
            var name = report.Name.Replace(' ', '_').Replace("(", "").Replace(")", "") + "_" + report.Period.Replace(' ', '_') + ".csv";
            return File(Encoding.UTF8.GetBytes(report.Content ?? ""), "text/csv", name);
        }
    }
}
