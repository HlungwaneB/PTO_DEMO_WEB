using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using PTO_Management_System.Interfaces;

namespace PTO_Management_System.Controllers
{
    /// <summary>
    /// The ONLY way to open an uploaded document. Beneficiaries can open their own documents,
    /// the administrator and officers can open any (SRS 5.1 "Controlled access").
    /// </summary>
    [Authorize]
    public class DocumentsController : Controller
    {
        private readonly IPtoDocument _documents;
        private readonly IPtoApplication _applications;
        private readonly IBeneficiary _beneficiaries;
        private readonly IFileStorage _storage;
        

        public DocumentsController(IPtoDocument documents, IPtoApplication applications, IBeneficiary beneficiaries,
            IFileStorage storage)
        {
            _documents = documents;
            _applications = applications;
            _beneficiaries = beneficiaries;
            _storage = storage;
            
        }

        public async Task<IActionResult> Download(int id)
        {
            var document = _documents.GetById(id);
            if (document == null) return NotFound();

            bool isStaff = User.IsInRole(WebConstants.RoleAdmin);
            if (!isStaff)
            {
                var me = _beneficiaries.ByUserId(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var application = _applications.GetById(document.PtoApplicationId);
                if (me == null || application == null || application.BeneficiaryId != me.Id)
                    return Forbid();
            }

            var stream = await _storage.OpenReadAsync(document.StoredFileName);
            if (stream == null) return NotFound();

            return File(stream, document.ContentType ?? "application/octet-stream", document.OriginalFileName);

        }
    }
}
