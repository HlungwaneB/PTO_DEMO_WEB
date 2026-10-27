using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using PTO_Management_System.Interfaces;
using PTO_Management_System.Models;
using PTO_Management_System.Validation;
using PTO_Management_System.Services;

namespace PTO_Management_System.Controllers
{
    /// <summary>
    /// PTO application wizard, part 2:
    ///   19 Dependent information -> NEW Create Password -> 12 Documents -> 13 Review -> 14 Submitted
    /// If the applicant has no dependants the wizard goes from Contact straight to Create Password.
    /// The account (login = ID number, password = the one created here) is created on Create Password,
    /// so the applicant can log in later and finish or track the application.
    /// </summary>
    public class RegisterController : Controller
    {
        private readonly IBeneficiary _beneficiaries;
        private readonly IPtoApplication _applications;
        private readonly IPtoDocument _documents;
        private readonly IFileStorage _storage;
        private readonly ISupabaseAuth _auth;
        private readonly SessionService _session;
        private readonly ILogger<RegisterController> _logger;

        public RegisterController(IBeneficiary beneficiaries, IPtoApplication applications, IPtoDocument documents,
            IFileStorage storage, ISupabaseAuth auth, SessionService session,
            ILogger<RegisterController> logger)
        {
            _beneficiaries = beneficiaries;
            _applications = applications;
            _documents = documents;
            _storage = storage;
            _auth = auth;
            _session = session;
            _logger = logger;
        }

        private WizardState GetState()
        {
            return HttpContext.Session.GetJson<WizardState>(WebConstants.WizardSessionKey) ?? new WizardState();
        }

        private void SaveState(WizardState state)
        {
            HttpContext.Session.SetJson(WebConstants.WizardSessionKey, state);
        }

        /// <summary>True when the first three wizard screens were completed.</summary>
        private static bool WizardComplete(WizardState s)
        {
            return s.Eligible && s.Personal != null && s.Contact != null;
        }

        // ------------------------------------------------------------ Screen 19: Dependent information

        [HttpGet]
        public IActionResult Dependants()
        {
            var state = GetState();
            if (!WizardComplete(state)) return RedirectToAction("Eligibility", "Apply");
            return View(new DependantsPageViewModel { Added = state.Dependants });
        }

        /// <param name="command">add = save and show an empty form; continue = save and go to Create Password</param>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Dependants(DependantsPageViewModel page, string command)
        {
            var state = GetState();
            if (!WizardComplete(state)) return RedirectToAction("Eligibility", "Apply");

            var form = page.Form ?? new DependantViewModel();

            // "Continue" with an empty form = the applicant is finished adding dependants
            if (command == "continue" && !form.HasAnyData)
            {
                ModelState.Clear();
                if (state.Dependants.Count == 0)
                {
                    ModelState.AddModelError("", "Add at least one dependant, or click Back and answer NO to \"Do you have dependants?\".");
                    return View(new DependantsPageViewModel { Added = state.Dependants });
                }
                return RedirectToAction(nameof(CreatePassword));
            }

            if (!ModelState.IsValid)
            {
                page.Added = state.Dependants;
                return View(page);
            }

            // Verification: no duplicate ID numbers
            if (!string.IsNullOrWhiteSpace(form.IdNumber))
            {
                if (form.IdNumber == state.Personal.IdNumber)
                    ModelState.AddModelError("Form.IdNumber", "A dependant cannot have the same ID number as the applicant.");
                else if (state.Dependants.Any(d => d.IdNumber == form.IdNumber))
                    ModelState.AddModelError("Form.IdNumber", "This dependant was already added.");
            }
            if (!ModelState.IsValid)
            {
                page.Added = state.Dependants;
                return View(page);
            }

            if (!string.IsNullOrWhiteSpace(form.Cellphone)) form.Cellphone = PhoneHelper.Normalize(form.Cellphone);
            state.Dependants.Add(form);
            SaveState(state);

            if (command == "add")
            {
                ModelState.Clear(); // show an empty form for the next dependant
                return View(new DependantsPageViewModel { Added = state.Dependants });
            }
            return RedirectToAction(nameof(CreatePassword));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RemoveDependant(int index)
        {
            var state = GetState();
            if (index >= 0 && index < state.Dependants.Count)
            {
                state.Dependants.RemoveAt(index);
                SaveState(state);
            }
            return RedirectToAction(nameof(Dependants));
        }

        // ------------------------------------------------------------ NEW screen: Create Password

        [HttpGet]
        public IActionResult CreatePassword()
        {
            var state = GetState();
            if (!WizardComplete(state)) return RedirectToAction("Eligibility", "Apply");
            if (state.Contact.HasDependants == true && state.Dependants.Count == 0)
                return RedirectToAction(nameof(Dependants));

            return View(new CreatePasswordViewModel
            {
                IdNumber = state.Personal.IdNumber,
                HasDependants = state.Dependants.Count > 0
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePassword(CreatePasswordViewModel model)
        {
            var state = GetState();
            if (!WizardComplete(state)) return RedirectToAction("Eligibility", "Apply");

            model.IdNumber = state.Personal.IdNumber; // never trust a posted ID number
            model.HasDependants = state.Dependants.Count > 0;
            if (!ModelState.IsValid) return View(model);

            // Verification again (somebody may have registered while this page was open)
            if (_beneficiaries.IdNumberExists(model.IdNumber))
            {
                ModelState.AddModelError("", "An application already exists for this ID number. Please log in instead.");
                return View(model);
            }

            // 1. Create the login in Supabase Auth (the e-mail + the password just created).
            var passwordProblem = PasswordRules.Check(model.Password);
            if (passwordProblem != null)
            {
                ModelState.AddModelError("", passwordProblem);
                return View(model);
            }

            const string emailTaken = "This e-mail address already has an account. Please log in, or use another e-mail address.";
            var loginEmail = state.Contact.Email.Trim();
            AuthSession session;
            try
            {
                session = await _auth.SignUpAsync(loginEmail, model.Password,
                (state.Personal.FirstNames + " " + state.Personal.Surname).Trim());
            }
            catch (AuthException ex) when (ex.Code == "user_already_exists")
            {
                // A leftover login from an earlier attempt that stopped half-way? Reuse it when the
                // password matches and it has no application yet.
                try { session = await _auth.SignInAsync(loginEmail, model.Password); }
                catch (AuthException)
                {
                    ModelState.AddModelError("", emailTaken);
                    return View(model);
                }
                if (_beneficiaries.ByUserId(session.UserId) != null)
                {
                    ModelState.AddModelError("", emailTaken);
                    return View(model);
                }
            }
            catch (AuthException ex)
            {
                _logger.LogError(ex, "Could not create the Supabase login for {Id}", state.Personal.IdNumber);
                ModelState.AddModelError("", ex.IsRateLimited ? "Too many attempts. Please wait a few minutes and try again." : ex.Message);
                return View(model);
            }

            // 2. Save the beneficiary, the dependants and an (empty) draft application
            try
            {
                var p = state.Personal;
                var c = state.Contact;
                var e = state.Eligibility;

                var beneficiary = new Beneficiary
                {
                    UserId = session.UserId,
                    IdNumber = p.IdNumber,
                    Title = p.Title,
                    Initials = p.Initials.Trim().ToUpper(),
                    Surname = p.Surname.Trim(),
                    FirstNames = p.FirstNames.Trim(),
                    DateOfBirth = p.DateOfBirth.Value.Date,
                    Gender = p.Gender,
                    Cellphone = c.Cellphone,
                    HomeCellphone = c.HomeCellphone,
                    Email = c.Email.Trim(),
                    MonthlyIncome = e.MonthlyIncome ?? 0,
                    IsSouthAfrican = e.IsSouthAfrican == true,
                    IsFirstTimeOwner = e.IsFirstTimeOwner == true,
                    IsOver21 = e.IsOver21 == true,
                    IsFirstTimeSubsidy = e.IsFirstTimeSubsidy == true,
                    IsMilitaryVeteran = e.IsMilitaryVeteran,
                    IsElderly = e.IsElderly
                };

                foreach (var d in state.Dependants)
                {
                    beneficiary.Dependants.Add(new Dependant
                    {
                        Surname = d.Surname.Trim(),
                        Initials = d.Initials.Trim().ToUpper(),
                        FirstNames = d.FirstNames.Trim(),
                        Gender = d.Gender,
                        IdNumber = string.IsNullOrWhiteSpace(d.IdNumber) ? null : d.IdNumber,
                        Cellphone = d.Cellphone,
                        Email = d.Email
                    });
                }

                // Beneficiary + dependants + draft application are saved together in ONE SaveChanges,
                // so we never end up with half a record.
                beneficiary.Applications.Add(new PtoApplication
                {
                    ReferenceNumber = beneficiary.IdNumber,   // the reference IS the ID number
                    Settlement = c.Settlement,                // the area (limited sites) applied for
                    Status = PtoStatus.Draft,
                    CreatedAt = DateTime.UtcNow
                });
                _beneficiaries.Create(beneficiary);
            }
            catch (Exception ex)
            {
                // Undo the login so the applicant can simply try again
                _logger.LogError(ex, "Could not save the beneficiary for {Id}", state.Personal.IdNumber);
                
                ModelState.AddModelError("", "We could not save your application. Please try again.");
                return View(model);
            }

            // 3. Wizard data is no longer needed; sign the new user in and continue with the documents
            HttpContext.Session.Remove(WebConstants.WizardSessionKey);
            await _session.SignInAsync(session, WebConstants.RoleUser, persistent: false);
            return RedirectToAction(nameof(Documents));
        }

        // ------------------------------------------------------------ Screen 12: Documents

        [Authorize(Roles = WebConstants.RoleUser)]
        [HttpGet]
        public IActionResult Documents()
        {
            var me = Current();
            if (me.application == null) return RedirectToAction("Eligibility", "Apply");
            if (me.application.Status != PtoStatus.Draft) return RedirectToAction("Application", "MyPto");
            return View(BuildDocuments(me.beneficiary, me.application));
        }

        [Authorize(Roles = WebConstants.RoleUser)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(string documentType, IFormFile file)
        {
            var me = Current();
            if (me.application == null || me.application.Status != PtoStatus.Draft)
                return RedirectToAction("Documents");

            var allowed = new[] { WebConstants.DocCertifiedId, WebConstants.DocProofOfIncome, WebConstants.DocBirthCertificates };
            if (!allowed.Contains(documentType))
            {
                TempData["Error"] = "Unknown document type.";
                return RedirectToAction("Documents");
            }

            // Verification: size, extension and real file signature
            var problem = await DocumentValidator.ValidateAsync(file);
            if (problem != null)
            {
                TempData["Error"] = problem;
                return RedirectToAction("Documents");
            }

            try
            {
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                var storedName = User.FindFirstValue(ClaimTypes.NameIdentifier) + "-" + Guid.NewGuid().ToString("N") + extension;
                await _storage.SaveAsync(file, storedName);

                // Replace an earlier upload of the same type
                var old = me.application.Documents.FirstOrDefault(d => d.DocumentType == documentType);
                if (old != null)
                {
                    _documents.Remove(old.Id);
                    await _storage.DeleteAsync(old.StoredFileName);
                }

                _documents.Add(new PtoDocument
                {
                    PtoApplicationId = me.application.Id,
                    DocumentType = documentType,
                    OriginalFileName = Path.GetFileName(file.FileName),
                    StoredFileName = storedName,
                    ContentType = extension == ".pdf" ? "application/pdf" : extension == ".png" ? "image/png" : "image/jpeg",
                    SizeBytes = file.Length,
                    VerificationStatus = "Verified" // the system verified size, type and file signature just now
                });
                TempData["Message"] = documentType + " uploaded and verified.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Upload failed");
                TempData["Error"] = "The file could not be saved. Please try again.";
            }
            return RedirectToAction("Documents");
        }

        // ------------------------------------------------------------ Screen 13: Review and submit

        [Authorize(Roles = WebConstants.RoleUser)]
        [HttpGet]
        public IActionResult Review()
        {
            var me = Current();
            if (me.application == null) return RedirectToAction("Eligibility", "Apply");
            if (me.application.Status != PtoStatus.Draft) return RedirectToAction("Application", "MyPto");

            var docs = BuildDocuments(me.beneficiary, me.application);
            if (!docs.AllRequiredUploaded)
            {
                TempData["Error"] = "Upload all the required documents before you continue.";
                return RedirectToAction("Documents");
            }
            return View(BuildReview(me.beneficiary, me.application, docs));
        }

        [Authorize(Roles = WebConstants.RoleUser)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Submit()
        {
            var me = Current();
            if (me.application == null) return RedirectToAction("Eligibility", "Apply");

            var docs = BuildDocuments(me.beneficiary, me.application);
            if (!docs.AllRequiredUploaded)
            {
                TempData["Error"] = "Upload all the required documents before you submit.";
                return RedirectToAction("Documents");
            }

            try
            {
                _applications.Submit(me.application.Id, me.beneficiary.IdNumber);
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Application", "MyPto");
            }
            return RedirectToAction(nameof(Submitted));
        }

        // ------------------------------------------------------------ Screen 14: Application submitted

        [Authorize(Roles = WebConstants.RoleUser)]
        public IActionResult Submitted()
        {
            var me = Current();
            if (me.application == null || me.application.Status == PtoStatus.Draft)
                return RedirectToAction("Documents");
            return View(me.application);
        }

        // ------------------------------------------------------------ helpers

        private (Beneficiary beneficiary, PtoApplication application) Current()
        {
            var beneficiary = _beneficiaries.ByUserId(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var application = beneficiary == null ? null : _applications.ByBeneficiaryId(beneficiary.Id);
            return (beneficiary, application);
        }

        private DocumentsPageViewModel BuildDocuments(Beneficiary beneficiary, PtoApplication application)
        {
            var rows = new List<(string type, bool required)>
            {
                (WebConstants.DocCertifiedId, true),
                (WebConstants.DocProofOfIncome, true),
                (WebConstants.DocBirthCertificates, beneficiary.Dependants.Count > 0)
            };

            var page = new DocumentsPageViewModel();
            foreach (var row in rows)
            {
                page.Rows.Add(new DocumentRow
                {
                    DocumentType = row.type,
                    Required = row.required,
                    Uploaded = application.Documents.FirstOrDefault(d => d.DocumentType == row.type)
                });
            }
            return page;
        }

        private static ReviewViewModel BuildReview(Beneficiary b, PtoApplication app, DocumentsPageViewModel docs)
        {
            int met = new[] { b.MonthlyIncome < WebConstants.MaxMonthlyIncome, b.IsSouthAfrican, b.IsFirstTimeOwner,
                              b.IsOver21, b.IsFirstTimeSubsidy }.Count(x => x);
            return new ReviewViewModel
            {
                Beneficiary = b,
                Application = app,
                DocumentsAttached = docs.Rows.Count(r => r.Uploaded != null),
                DocumentsRequired = docs.Rows.Count(r => r.Required),
                RequirementsMet = met
            };
        }
    }
}
