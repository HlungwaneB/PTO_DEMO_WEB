using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PTO_Management_System.Interfaces;
using PTO_Management_System.Models;

namespace PTO_Management_System.Controllers
{
    /// <summary>Screen 39: audit log (SRS 7.1 / FR15). Administrator only.</summary>
    [Authorize(Roles = WebConstants.RoleAdmin)]
    public class AuditController : Controller
    {
        private readonly IAuditLog _audit;

        public AuditController(IAuditLog audit)
        {
            _audit = audit;
        }

        public IActionResult Index(string search = null, int page = 1)
        {
            var query = _audit.GetAll();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(a => a.UserName.ToLower().Contains(s) || a.Action.ToLower().Contains(s)
                                         || (a.RecordRef != null && a.RecordRef.ToLower().Contains(s)));
            }
            ViewBag.Search = search;
            return View(PaginatedList<AuditLog>.Create(query.OrderByDescending(a => a.When), page, 12));
        }
    }
}
