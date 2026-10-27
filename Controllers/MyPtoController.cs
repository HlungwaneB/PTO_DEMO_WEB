using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using PTO_Management_System.Interfaces;
using PTO_Management_System.Models;
using PTO_Management_System.Validation;

namespace PTO_Management_System.Controllers
{
    /// <summary>
    /// Everything a beneficiary sees after logging in (screens 05, 15, 16, 17, 18, 20, 21):
    /// Home, My PTO, Track, Renewal, Notifications and Profile.
    /// </summary>
    [Authorize(Roles = WebConstants.RoleUser)]
    public class MyPtoController : Controller
    {
        private readonly IBeneficiary _beneficiaries;
        private readonly IPtoApplication _applications;
        private readonly INotification _notifications;
        private readonly ISite _sites;
        

        public MyPtoController(IBeneficiary beneficiaries, IPtoApplication applications,
            INotification notifications, ISite sites)
        {
            _sites = sites;
            _beneficiaries = beneficiaries;
            _applications = applications;
            _notifications = notifications;
           
        }

        private Beneficiary Me()
        {
            return _beneficiaries.ByUserId(User.FindFirstValue(ClaimTypes.NameIdentifier)); 
        }

        // ------------------------------------------------------------ Screen 05: Home

        public IActionResult Index()
        {
            var me = Me();
            if (me == null) return RedirectToAction("SignOutNow", "Account");
            var app = _applications.ByBeneficiaryId(me.Id);

            // Still busy with the application? Send them back to the documents step
            if (app != null && app.Status == PtoStatus.Draft) return RedirectToAction("Documents", "Register");

            return View(new HomeViewModel
            {
                Beneficiary = me,
                Application = app,
                UnreadNotifications = _notifications.UnreadCount(me.Id),
                ShowExpiryAlert = app != null && (app.Status == PtoStatus.Expiring || app.Status == PtoStatus.Expired)
            });
        }

        // ------------------------------------------------------------ Screen 15: My PTO

        public IActionResult Application()
        {
            var me = Me();
            if (me == null) return RedirectToAction("SignOutNow", "Account");
            var app = _applications.ByBeneficiaryId(me.Id);
            if (app == null) return RedirectToAction("Eligibility", "Apply");
            app.Beneficiary = me;
            // For a Pending application show how many applications are waiting in the area and the target
            ViewBag.Area = _sites.Areas().FirstOrDefault(a => a.Area.Name == app.Settlement);
            return View(app);
        }

        // ------------------------------------------------------------ Screen 17: Track PTO

        public IActionResult Track()
        {
            var me = Me();
            if (me == null) return RedirectToAction("SignOutNow", "Account");
            var app = _applications.ByBeneficiaryId(me.Id);
            if (app == null) return RedirectToAction("Eligibility", "Apply");

            return View(new TrackViewModel
            {
                Application = app,
                Steps = BuildSteps(app),
                PathwayStage = PtoStatus.PathwayStage(app.Status),
                PathwayText = PtoStatus.PathwayText(app.Status)
            });
        }

        /// <summary>Builds the timeline: every step is done, now or next.</summary>
        private static List<TrackStep> BuildSteps(PtoApplication app)
        {
            string s = app.Status;
            bool submitted = app.SubmittedAt.HasValue;
            bool decided = s != PtoStatus.Draft && s != PtoStatus.Pending;
            bool approved = app.ApprovedAt.HasValue;

            var steps = new List<TrackStep>
            {
                new TrackStep
                {
                    Title = "Application submitted",
                    Detail = submitted ? app.SubmittedAt.Value.ToString("dd MMM yyyy") : "Not submitted yet",
                    State = submitted ? "done" : "now"
                },
                new TrackStep
                {
                    Title = "Documents verified",
                    Detail = app.Documents.Count > 0 && app.Documents.All(d => d.VerificationStatus == "Verified")
                        ? "All documents verified by the system" : "The system is checking your documents",
                    State = !submitted ? "next" : (decided ? "done" : "now")
                },
                new TrackStep
                {
                    Title = s == PtoStatus.Rejected ? "Application not approved" : "Automatic decision",
                    Detail = s == PtoStatus.Rejected ? (app.RejectReason ?? "Not approved")
                        : approved ? "Approved automatically on " + app.ApprovedAt.Value.ToString("dd MMM yyyy") : "Waiting: the system decides when " + app.Settlement + " has enough applications",
                    State = decided ? "done" : (submitted ? "next" : "next")
                }
            };

            if (s != PtoStatus.Rejected)
            {
                steps.Add(new TrackStep
                {
                    Title = "Site allocated",
                    Detail = app.Site != null ? "Site " + app.Site.SiteNumber + ", " + app.Site.Settlement : "Not allocated yet",
                    State = app.Site != null ? "done" : "next"
                });
                steps.Add(new TrackStep
                {
                    Title = "PTO valid",
                    Detail = app.ExpiryDate.HasValue ? "Valid until " + app.ExpiryDate.Value.ToString("dd MMM yyyy") : "Starts when approved",
                    State = s == PtoStatus.Active || s == PtoStatus.Renewed ? "now"
                          : (s == PtoStatus.Expiring || s == PtoStatus.Expired || s == PtoStatus.Deregistered) ? "done" : "next"
                });
            }
            return steps;
        }

        // ------------------------------------------------------------ Screen 16: Renewal request

        [HttpGet]
        public IActionResult Renewal()
        {
            var me = Me();
            var app = me == null ? null : _applications.ByBeneficiaryId(me.Id);
            if (app == null) return RedirectToAction(nameof(Index));
            if (!CanRenew(app))
            {
                TempData["Error"] = "Renewal is only available when your PTO is Active, Expiring or Expired.";
                return RedirectToAction(nameof(Application));
            }
            return View(new RenewalViewModel { Application = app });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Renewal(RenewalViewModel model)
        {
            var me = Me();
            var app = me == null ? null : _applications.ByBeneficiaryId(me.Id);
            if (app == null) return RedirectToAction(nameof(Index));
            model.Application = app;

            if (!CanRenew(app))
            {
                TempData["Error"] = "Renewal is only available when your PTO is Active, Expiring or Expired.";
                return RedirectToAction(nameof(Application));
            }
            if (model.HouseholdChanged == true && !model.NewIncome.HasValue)
                ModelState.AddModelError(nameof(model.NewIncome), "Enter your new monthly income.");
            if (!ModelState.IsValid) return View(model);

            try
            {
                // The SYSTEM decides at once: Approved (PTO renewed) or Declined (with the reason)
                var result = _applications.Renew(app.Id, model.StillLiving == true, model.HouseholdChanged == true,
                    model.NewIncome, model.Notes);
                if (result.Status == "Approved") TempData["Message"] = "Your PTO was renewed.";
                else TempData["Error"] = "Renewal declined. " + result.Notes;
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction(nameof(Application));
        }

        private static bool CanRenew(PtoApplication app)
        {
            return app.Status == PtoStatus.Active || app.Status == PtoStatus.Expiring
                || app.Status == PtoStatus.Expired || app.Status == PtoStatus.Renewed;
        }

        // ------------------------------------------------------------ De-register (the beneficiary releases the site)

        [HttpGet]
        public IActionResult Deregister()
        {
            var me = Me();
            var app = me == null ? null : _applications.ByBeneficiaryId(me.Id);
            if (app == null || !CanDeregister(app)) return RedirectToAction(nameof(Application));
            return View(new DeregisterViewModel { Application = app });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Deregister(DeregisterViewModel model)
        {
            var me = Me();
            var app = me == null ? null : _applications.ByBeneficiaryId(me.Id);
            if (app == null || !CanDeregister(app)) return RedirectToAction(nameof(Application));
            model.Application = app;
            if (!ModelState.IsValid) return View(model);

            _applications.Deregister(app.Id, model.Reason, "Beneficiary " + app.ReferenceNumber);
            TempData["Message"] = "Your PTO was de-registered and the site was released.";
            return RedirectToAction(nameof(Application));
        }

        private static bool CanDeregister(PtoApplication app)
        {
            return app.Status == PtoStatus.Pending || app.Status == PtoStatus.Active || app.Status == PtoStatus.Expiring
                || app.Status == PtoStatus.Expired || app.Status == PtoStatus.Renewed;
        }

        // ------------------------------------------------------------ Re-apply for another area after a rejection

        [HttpGet]
        public IActionResult Reapply()
        {
            var me = Me();
            var app = me == null ? null : _applications.ByBeneficiaryId(me.Id);
            if (app == null || app.Status != PtoStatus.Rejected && app.Status != PtoStatus.Deregistered) return RedirectToAction(nameof(Application));
            return View(new ReapplyViewModel { Application = app, Areas = _sites.Areas().Where(a => a.FreeSites > 0).ToList() });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Reapply(ReapplyViewModel model)
        {
            var me = Me();
            var app = me == null ? null : _applications.ByBeneficiaryId(me.Id);
            if (app == null || app.Status != PtoStatus.Rejected && app.Status != PtoStatus.Deregistered) return RedirectToAction(nameof(Application));

            model.Application = app;
            model.Areas = _sites.Areas().Where(a => a.FreeSites > 0).ToList();
            if (!ModelState.IsValid) return View(model);

            try
            {
                _applications.Reapply(app.Id, model.Settlement);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(nameof(model.Settlement), ex.Message);
                return View(model);
            }
            // Documents are still verified, so go to the review page and submit again
            return RedirectToAction("Review", "Register");
        }

        // ------------------------------------------------------------ Screen 20: Notifications

        public IActionResult Notifications()
        {
            var me = Me();
            if (me == null) return RedirectToAction("SignOutNow", "Account");

            var list = _notifications.ForBeneficiary(me.Id).ToList();
            // Remember which ones were unread BEFORE we mark them read, so the page can still highlight them
            ViewBag.UnreadIds = list.Where(n => !n.IsRead).Select(n => n.Id).ToList();
            _notifications.MarkAllRead(me.Id);
            return View(list);
        }

        // ------------------------------------------------------------ Screen 18: Update personal information

        [HttpGet]
        public IActionResult Profile()
        {
            var me = Me();
            if (me == null) return RedirectToAction("SignOutNow", "Account");
            return View(ToProfile(me));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(ProfileViewModel model)
        {
            var me = Me();
            if (me == null) return RedirectToAction("SignOutNow", "Account");

            // These come from the database, never from the form
            model.FullName = me.FullName;
            model.IdNumber = me.IdNumber;
            model.DependantsCount = me.Dependants.Count;

            if (!ModelState.IsValid) return View(model);

            var phone = PhoneHelper.Normalize(model.Cellphone);
            var email = model.Email.Trim();

            if (_beneficiaries.CellphoneExists(phone, me.Id))
                ModelState.AddModelError(nameof(model.Cellphone), "This cellphone number belongs to another applicant.");
            if (_beneficiaries.EmailExists(email, me.Id))
                ModelState.AddModelError(nameof(model.Email), "This e-mail address belongs to another applicant.");
            if (!ModelState.IsValid) return View(model);

          
            me.Cellphone = phone;
            me.Email = email;
            me.Address = string.IsNullOrWhiteSpace(model.Address) ? null : model.Address.Trim();
            _beneficiaries.Edit(me);

            TempData["Message"] = "Your information was updated.";
            return RedirectToAction(nameof(Profile));
        }

        private static ProfileViewModel ToProfile(Beneficiary b)
        {
            return new ProfileViewModel
            {
                FullName = b.FullName,
                IdNumber = b.IdNumber,
                DependantsCount = b.Dependants.Count,
                Cellphone = b.Cellphone,
                Email = b.Email,
                Address = b.Address
            };
        }
    }
}
