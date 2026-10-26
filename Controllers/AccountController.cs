using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
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
        private readonly IEmailSender _email;
        private readonly IMemoryCache _cache;
        private readonly ILogger<AccountController> _logger;
        private const string ResetPurpose = "PasswordResetCode";
        public AccountController(SignInManager<IdentityUser> signInManager, UserManager<IdentityUser> userManager,
            IBeneficiary beneficiaries, IPtoApplication applications, IAuditLog audit,
            IEmailSender email, IMemoryCache cache, ILogger<AccountController> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
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
        // ------------------------------------------------------------ Forgot password (e-mail code)

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

    if (!_email.IsEnabled)
    {
        ModelState.AddModelError("", "E-mail is not available right now. Please contact the municipality.");
        return View(model);
    }

    var identifier = model.Identifier.Trim();
    var user = await FindUserForReset(identifier);

    // One code per minute per account, and only if an e-mail address is on file
    if (user != null && !string.IsNullOrWhiteSpace(user.Email) && !_cache.TryGetValue("fp:" + user.Id, out _))
    {
        try
        {
            var code = await _userManager.GenerateUserTokenAsync(user, TokenOptions.DefaultEmailProvider, ResetPurpose);
            var name = _beneficiaries.ByUserId(user.Id)?.FullName ?? "";
            await _email.SendCodeAsync(user.Email, name, code);
            _cache.Set("fp:" + user.Id, true, TimeSpan.FromSeconds(60));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not send the password reset code.");
        }
    }

    // Same answer whether or not the account exists
    HttpContext.Session.SetString("ResetIdentifier", identifier);
    return RedirectToAction("ResetPassword");
}

[HttpGet]
public IActionResult ResetPassword()
{
    var identifier = HttpContext.Session.GetString("ResetIdentifier");
    if (string.IsNullOrEmpty(identifier)) return RedirectToAction("ForgotPassword");
    return View(new ResetPasswordViewModel { Identifier = identifier });
}

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
{
    // The account comes from the session, not from the form, so it cannot be changed by the user
    var identifier = HttpContext.Session.GetString("ResetIdentifier");
    if (string.IsNullOrEmpty(identifier)) return RedirectToAction("ForgotPassword");
    model.Identifier = identifier;

    if (!ModelState.IsValid)
    {
        model.Password = null; model.ConfirmPassword = null;
        return View(model);
    }

    const string wrong = "That code is not correct or has expired. Go back and request a new code.";
    var user = await FindUserForReset(identifier);
    if (user == null)
    {
        ModelState.AddModelError("", wrong);
        return View(model);
    }

    if (await _userManager.IsLockedOutAsync(user))
    {
        ModelState.AddModelError("", "Too many failed attempts. Please try again in 10 minutes.");
        return View(model);
    }

    bool valid = await _userManager.VerifyUserTokenAsync(user, TokenOptions.DefaultEmailProvider, ResetPurpose, model.Code);
    if (!valid)
    {
        await _userManager.AccessFailedAsync(user); // counts towards the 5-attempt lockout
        ModelState.AddModelError("", wrong);
        model.Password = null; model.ConfirmPassword = null;
        return View(model);
    }

    var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
    var result = await _userManager.ResetPasswordAsync(user, resetToken, model.Password);
    if (!result.Succeeded)
    {
        foreach (var error in result.Errors) ModelState.AddModelError("", error.Description);
        model.Password = null; model.ConfirmPassword = null;
        return View(model); // the code is still valid, so they can choose a stronger password
    }

    await _userManager.ResetAccessFailedCountAsync(user);
    HttpContext.Session.Remove("ResetIdentifier");
    _audit.Log("Beneficiary", "Password reset with e-mail code", identifier);

    try
    {
        var name = _beneficiaries.ByUserId(user.Id)?.FullName ?? "";
        await _email.SendAsync(user.Email, name, "myPTO: your password was changed", "Your password was changed",
            "The password for your myPTO account was just changed. If this was not you, contact the Mangaung municipality immediately.");
    }
    catch (Exception ex) { _logger.LogError(ex, "Could not send the password-changed e-mail."); }

    TempData["Message"] = "Your password was changed. Log in with your new password.";
    bool staff = await _userManager.IsInRoleAsync(user, WebConstants.RoleAdmin)
              || await _userManager.IsInRoleAsync(user, WebConstants.RoleOfficer);
    return RedirectToAction(staff ? "AdminLogin" : "Login");
}

private async Task<IdentityUser> FindUserForReset(string identifier)
{
    identifier = (identifier ?? "").Trim();
    var beneficiary = _beneficiaries.ByLoginIdentifier(identifier);
    if (beneficiary != null) return await _userManager.FindByIdAsync(beneficiary.UserId);
    if (identifier.Contains("@")) return await _userManager.FindByEmailAsync(identifier);
    return null;
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
                if (user != null && !(await _userManager.IsInRoleAsync(user, WebConstants.RoleAdmin)))
                    user = null; // only the administrator can use this page
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
                    if (staffUser != null && await _userManager.IsInRoleAsync(staffUser, WebConstants.RoleAdmin))
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
