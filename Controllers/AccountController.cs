using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using PTO_Management_System.Interfaces;
using PTO_Management_System.Models;
using PTO_Management_System.Services;

namespace PTO_Management_System.Controllers
{
    /// <summary>
    /// Screens 02 Log in, 04 Administrator log in, 06 Search for your PTO, 08 Application not found,
    /// plus Forgot password. Every login is a Supabase Auth login (the same as the Flutter app).
    /// Beneficiaries log in with their ID number (or e-mail) and password; the administrator with an e-mail.
    /// </summary>
    public class AccountController : Controller
    {
        private const string GenericError = "Incorrect ID number or password.";
        private const string StaffError = "Incorrect e-mail address or password.";
        private const string WrongCode = "That code is not correct or has expired. Go back and request a new code.";

        private readonly ISupabaseAuth _auth;
        private readonly SessionService _session;
        private readonly IProfile _profiles;
        private readonly IBeneficiary _beneficiaries;
        private readonly IPtoApplication _applications;
        private readonly IAuditLog _audit;
        private readonly IEmailSender _email;
        private readonly IMemoryCache _cache;
        private readonly ILogger<AccountController> _logger;

        public AccountController(ISupabaseAuth auth, SessionService session, IProfile profiles,
            IBeneficiary beneficiaries, IPtoApplication applications, IAuditLog audit,
            IEmailSender email, IMemoryCache cache, ILogger<AccountController> logger)
        {
            _auth = auth;
            _session = session;
            _profiles = profiles;
            _beneficiaries = beneficiaries;
            _applications = applications;
            _audit = audit;
            _email = email;
            _cache = cache;
            _logger = logger;
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
            await _session.SignOutAsync();
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _session.SignOutAsync();
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

        // ------------------------------------------------------------ Forgot password (code sent by Supabase)

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View(new ForgotPasswordViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var email = await ResolveEmail(model.Identifier);

            // One code per minute per e-mail address
            if (!string.IsNullOrEmpty(email) && !_cache.TryGetValue("fp:" + email.ToLowerInvariant(), out _))
            {
                try
                {
                    await _auth.SendRecoveryCodeAsync(email);
                    _cache.Set("fp:" + email.ToLowerInvariant(), true, TimeSpan.FromSeconds(60));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Could not request the password reset code.");
                }
            }

            // Same answer whether or not the account exists
            HttpContext.Session.SetString("ResetEmail", email ?? "unknown");
            return RedirectToAction("ResetPassword");
        }

        [HttpGet]
        public IActionResult ResetPassword()
        {
            var email = HttpContext.Session.GetString("ResetEmail");
            if (string.IsNullOrEmpty(email)) return RedirectToAction("ForgotPassword");
            return View(new ResetPasswordViewModel { Identifier = email });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            // The account comes from the session, not from the form, so it cannot be changed by the user
            var email = HttpContext.Session.GetString("ResetEmail");
            if (string.IsNullOrEmpty(email)) return RedirectToAction("ForgotPassword");
            model.Identifier = email;

            if (!ModelState.IsValid)
            {
                model.Password = null; model.ConfirmPassword = null;
                return View(model);
            }

            // Check the password BEFORE the code is used up (a code works only once)
            var rule = PasswordRules.Check(model.Password);
            if (rule != null)
            {
                ModelState.AddModelError("", rule);
                model.Password = null; model.ConfirmPassword = null;
                return View(model);
            }

            AuthSession recovery;
            try
            {
                recovery = await _auth.VerifyRecoveryCodeAsync(email, model.Code);
            }
            catch (AuthException ex)
            {
                _logger.LogWarning("Reset code rejected: {Code}", ex.Code);
                ModelState.AddModelError("", ex.IsRateLimited ? "Too many attempts. Please wait a few minutes and try again." : WrongCode);
                model.Password = null; model.ConfirmPassword = null;
                return View(model);
            }

            try
            {
                await _auth.UpdatePasswordAsync(recovery.AccessToken, model.Password);
            }
            catch (AuthException ex)
            {
                ModelState.AddModelError("", ex.Message + " Please request a new code.");
                model.Password = null; model.ConfirmPassword = null;
                return View(model);
            }

            HttpContext.Session.Remove("ResetEmail");
            _audit.Log("Beneficiary", "Password reset with e-mail code", email);

            try
            {
                var name = _beneficiaries.ByUserId(recovery.UserId)?.FullName ?? "";
                await _email.SendAsync(email, name, "myPTO: your password was changed", "Your password was changed",
                    "The password for your myPTO account was just changed. If this was not you, contact the Mangaung municipality immediately.");
            }
            catch (Exception ex) { _logger.LogError(ex, "Could not send the password-changed e-mail."); }

            TempData["Message"] = "Your password was changed. Log in with your new password.";
            bool staff = _profiles.RoleFor(recovery.UserId) == WebConstants.RoleAdmin;
            return RedirectToAction(staff ? "AdminLogin" : "Login");
        }

        // ------------------------------------------------------------ helpers

        /// <summary>An ID number is turned into the e-mail the account was created with; an e-mail is used as typed.</summary>
        private async Task<string> ResolveEmail(string identifier)
        {
            identifier = (identifier ?? "").Trim();
            if (identifier.Length == 0) return null;
            if (identifier.Contains("@")) return identifier;
            try { return await _auth.EmailForIdNumberAsync(identifier); }
            catch (Exception ex) { _logger.LogError(ex, "ID number lookup failed."); return null; }
        }

        /// <summary>Returns null when the user is signed in, otherwise the message to show.</summary>
        private async Task<string> TrySignIn(LoginViewModel model, bool staff)
        {
            var email = await ResolveEmail(model.Identifier);
            if (string.IsNullOrEmpty(email)) return staff ? StaffError : GenericError;

            AuthSession session;
            try
            {
                session = await _auth.SignInAsync(email, model.Password);
            }
            catch (AuthException ex)
            {
                if (ex.IsRateLimited) return "Too many attempts. Please wait a few minutes and try again.";
                if (ex.Code == "invalid_credentials" || ex.Status == 400) return staff ? StaffError : GenericError;
                _logger.LogError(ex, "Supabase login failed.");
                return "We could not log you in right now. Please try again.";
            }

            var role = _profiles.RoleFor(session.UserId);
            if (staff && role != WebConstants.RoleAdmin) return StaffError;   // beneficiaries cannot use the administrator page
            if (!staff && role == WebConstants.RoleAdmin) return "Administrators must use the Administrator log in page.";

            await _session.SignInAsync(session, role, model.RememberMe);
            return null;
        }
    }
}
