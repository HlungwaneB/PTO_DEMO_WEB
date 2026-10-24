using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PTO_Management_System.Interfaces;
using PTO_Management_System.Models;

namespace PTO_Management_System.Controllers
{
    /// <summary>
    /// Screens 02 Log in, 04 Administrator log in, 06 Search for your PTO, 08 Application not found.
    /// Beneficiaries log in with their ID NUMBER (their reference) and the password they created
    /// during the application. Staff log in with their e-mail address.
    /// </summary>
    public class AccountController : Controller
    {
        private const string GenericError = "Incorrect ID number or password.";

        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IBeneficiary _beneficiaries;
        private readonly IPtoApplication _applications;
        private readonly IAuditLog _audit;

        public AccountController(SignInManager<IdentityUser> signInManager, UserManager<IdentityUser> userManager,
            IBeneficiary beneficiaries, IPtoApplication applications, IAuditLog audit)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _beneficiaries = beneficiaries;
            _applications = applications;
            _audit = audit;
        }

        // ------------------------------------------------------------ Screen 02: Log in (beneficiary)

        [HttpGet]
        public IActionResult Login(string returnUrl = null)
        {
            if (User.Identity != null && User.Identity.IsAuthenticated) return RedirectToAction("Index", "Home");
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var error = await TrySignIn(model, staff: false);
            if (error != null)
            {
                ModelState.AddModelError("", error);
                model.Password = null;
                return View(model);
            }

            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                return Redirect(model.ReturnUrl);

            return RedirectToAction("Index", "MyPto");
        }

        // ------------------------------------------------------------ Screen 04: Administrator log in

        [HttpGet]
        public IActionResult AdminLogin()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated) return RedirectToAction("Index", "Home");
            return View(new LoginViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdminLogin(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var error = await TrySignIn(model, staff: true);
            if (error != null)
            {
                ModelState.AddModelError("", error);
                model.Password = null;
                return View(model);
            }

            _audit.Log("Administrator", "Logged in", model.Identifier.Trim());
            return RedirectToAction("Dashboard", "Admin");
        }

        // ------------------------------------------------------------ Log out

        /// <summary>Used by redirects (a redirect is always a GET): signs out and returns to the welcome page.</summary>
        [HttpGet]
        public async Task<IActionResult> SignOutNow()
        {
            await _signInManager.SignOutAsync();
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Home");
        }

        public IActionResult AccessDenied()
        {
            return View();
        }

        // ------------------------------------------------------------ Screen 06: Search for your PTO

        [HttpGet]
        public IActionResult Search()
        {
            if (User.IsInRole(WebConstants.RoleUser)) return RedirectToAction("Application", "MyPto");
            return View(new LoginViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Search(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var beneficiary = _beneficiaries.ByLoginIdentifier(model.Identifier);
            if (beneficiary == null) return View("NotFoundApplication", model.Identifier);

            var error = await TrySignIn(model, staff: false);
            if (error != null)
            {
                ModelState.AddModelError("", error);
                model.Password = null;
                return View(model);
            }

            var application = _applications.ByBeneficiaryId(beneficiary.Id);
            if (application == null) return View("NotFoundApplication", model.Identifier);
            if (application.Status == PtoStatus.Draft) return RedirectToAction("Documents", "Register");

            return RedirectToAction("Application", "MyPto");
        }

        // ------------------------------------------------------------ helper

        /// <summary>Returns null when the user is signed in, otherwise the message to show.</summary>
        private async Task<string> TrySignIn(LoginViewModel model, bool staff)
        {
            var identifier = (model.Identifier ?? "").Trim();
            IdentityUser user = null;

            if (staff)
            {
                user = await _userManager.FindByEmailAsync(identifier);
                if (user != null && !(await _userManager.IsInRoleAsync(user, WebConstants.RoleAdmin) ||
                                      await _userManager.IsInRoleAsync(user, WebConstants.RoleOfficer)))
                    user = null; // beneficiaries cannot use the administrator page
            }
            else
            {
                var beneficiary = _beneficiaries.ByLoginIdentifier(identifier);
                if (beneficiary != null)
                {
                    user = await _userManager.FindByIdAsync(beneficiary.UserId);
                }
                else if (identifier.Contains("@"))
                {
                    var staffUser = await _userManager.FindByEmailAsync(identifier);
                    if (staffUser != null && (await _userManager.IsInRoleAsync(staffUser, WebConstants.RoleAdmin) ||
                                              await _userManager.IsInRoleAsync(staffUser, WebConstants.RoleOfficer)))
                        return "Administrators must use the Administrator log in page.";
                }
            }

            if (user == null) return staff ? "Incorrect e-mail address or password." : GenericError;

            var result = await _signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: true);
            if (result.Succeeded) return null;
            if (result.IsLockedOut) return "Too many failed attempts. Please try again in 10 minutes.";
            return staff ? "Incorrect e-mail address or password." : GenericError;
        }
    }
}
