using Microsoft.AspNetCore.Mvc;
using PTO_Management_System.Interfaces;
using PTO_Management_System.Models;
using PTO_Management_System.Validation;

namespace PTO_Management_System.Controllers
{
    /// <summary>
    /// PTO application wizard, part 1 (nobody has to be logged in yet):
    ///   09 Reliable information -> (20 Not eligible) -> 10 Personal details and ID -> 11 Contact
    /// Part 2 (dependants, create password, documents, review, submit) is in RegisterController.
    /// The answers are kept in the Session and are only saved when the password is created.
    /// </summary>
    public class ApplyController : Controller
    {
        private readonly IBeneficiary _beneficiaries;
        private readonly ISite _sites;

        public ApplyController(IBeneficiary beneficiaries, ISite sites)
        {
            _beneficiaries = beneficiaries;
            _sites = sites;
        }

        /// <summary>Areas that still have at least one free site (sites are limited).</summary>
        private List<AreaRow> OpenAreas()
        {
            return _sites.Areas().Where(a => a.FreeSites > 0).ToList();
        }

        private WizardState GetState()
        {
            return HttpContext.Session.GetJson<WizardState>(WebConstants.WizardSessionKey) ?? new WizardState();
        }

        private void SaveState(WizardState state)
        {
            HttpContext.Session.SetJson(WebConstants.WizardSessionKey, state);
        }

        public IActionResult Index()
        {
            return RedirectToAction(nameof(Eligibility));
        }

        // ------------------------------------------------------------ Screen 09: Reliable information

        [HttpGet]
        public IActionResult Eligibility()
        {
            if (User.IsInRole(WebConstants.RoleUser)) return RedirectToAction("Index", "MyPto");
            return View(GetState().Eligibility ?? new EligibilityViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Eligibility(EligibilityViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var state = GetState();
            state.Eligibility = model;
            state.Eligible = model.Qualifies();
            SaveState(state);

            return state.Eligible ? RedirectToAction(nameof(Personal)) : RedirectToAction(nameof(NotEligible));
        }

        // ------------------------------------------------------------ Screen 20: Not eligible

        public IActionResult NotEligible()
        {
            var answers = GetState().Eligibility;
            if (answers == null) return RedirectToAction(nameof(Eligibility));

            // Tell the person exactly which requirement was not met
            var reasons = new List<string>();
            if (!answers.MonthlyIncome.HasValue || answers.MonthlyIncome.Value >= WebConstants.MaxMonthlyIncome)
                reasons.Add("Your monthly income must be less than R3 500.");
            if (answers.IsSouthAfrican != true) reasons.Add("You must be a South African citizen or naturalised.");
            if (answers.IsFirstTimeOwner != true) reasons.Add("You must be a first-time home owner.");
            if (answers.IsOver21 != true) reasons.Add("You must be over 21.");
            if (answers.IsFirstTimeSubsidy != true) reasons.Add("You must be a first-time government subsidy recipient.");

            ViewBag.Reasons = reasons;
            return View();
        }

        // ------------------------------------------------------------ Screen 10: Personal details and ID

        [HttpGet]
        public IActionResult Personal()
        {
            var state = GetState();
            if (!state.Eligible) return RedirectToAction(nameof(Eligibility));
            return View(state.Personal ?? new PersonalViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Personal(PersonalViewModel model)
        {
            var state = GetState();
            if (!state.Eligible) return RedirectToAction(nameof(Eligibility));

            if (!ModelState.IsValid) return View(model);

            // Verification: one application per ID number
            if (_beneficiaries.IdNumberExists(model.IdNumber))
            {
                ModelState.AddModelError(nameof(model.IdNumber),
                    "An application already exists for this ID number. Please log in instead.");
                return View(model);
            }

            state.Personal = model;
            SaveState(state);
            return RedirectToAction(nameof(Contact));
        }

        // ------------------------------------------------------------ Screen 11: Contact

        [HttpGet]
        public IActionResult Contact()
        {
            var state = GetState();
            if (!state.Eligible) return RedirectToAction(nameof(Eligibility));
            if (state.Personal == null) return RedirectToAction(nameof(Personal));
            ViewBag.Areas = OpenAreas();
            return View(state.Contact ?? new ContactViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Contact(ContactViewModel model)
        {
            var state = GetState();
            if (!state.Eligible) return RedirectToAction(nameof(Eligibility));
            if (state.Personal == null) return RedirectToAction(nameof(Personal));

            ViewBag.Areas = OpenAreas();
            if (!ModelState.IsValid) return View(model);

            // Verification: the chosen area must still have free sites
            if (!OpenAreas().Any(a => a.Area.Name == model.Settlement))
            {
                ModelState.AddModelError(nameof(model.Settlement), "That area has no free sites. Choose another area.");
                return View(model);
            }

            // Verification: store phone numbers in one format and refuse numbers / e-mails already registered
            model.Cellphone = PhoneHelper.Normalize(model.Cellphone);
            model.ConfirmCellphone = model.Cellphone;
            if (!string.IsNullOrWhiteSpace(model.HomeCellphone))
                model.HomeCellphone = PhoneHelper.Normalize(model.HomeCellphone);

            if (_beneficiaries.EmailExists(model.Email))
                ModelState.AddModelError(nameof(model.Email), "This e-mail address is already registered.");
            if (_beneficiaries.CellphoneExists(model.Cellphone))
                ModelState.AddModelError(nameof(model.Cellphone), "This cellphone number is already registered.");
            if (!ModelState.IsValid) return View(model);

            state.Contact = model;
            if (model.HasDependants != true) state.Dependants.Clear();
            SaveState(state);

            // YES -> Dependent information, NO -> straight to Create Password
            return model.HasDependants == true
                ? RedirectToAction("Dependants", "Register")
                : RedirectToAction("CreatePassword", "Register");
        }

        // ------------------------------------------------------------ Cancel (every screen has a Cancel button)

        public IActionResult Cancel()
        {
            HttpContext.Session.Remove(WebConstants.WizardSessionKey);
            return RedirectToAction("Index", "Home");
        }
    }
}
