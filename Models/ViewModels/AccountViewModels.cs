using System.ComponentModel.DataAnnotations;

namespace PTO_Management_System.Models
{
    /// <summary>Log in / Admin log in / Search for your PTO. The user name is the ID number.</summary>
    public class LoginViewModel
    {
        [Display(Name = "ID number")]
        [Required(ErrorMessage = "Enter your ID number")]
        public string Identifier { get; set; }

        [Required(ErrorMessage = "Enter your password")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Display(Name = "Remember me")]
        public bool RememberMe { get; set; }

        public string ReturnUrl { get; set; }
    }
    public class ForgotPasswordViewModel
    {
        [Display(Name = "ID number or e-mail")]
        [Required(ErrorMessage = "Enter your ID number or e-mail address")]
        public string Identifier { get; set; }
    }

    public class ResetPasswordViewModel
    {
        public string Identifier { get; set; }

        [Display(Name = "Verification code")]
        [Required(ErrorMessage = "Enter the 6-digit code from your e-mail")]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "The code has 6 digits")]
        public string Code { get; set; }

        [Display(Name = "New password")]
        [Required(ErrorMessage = "Enter a new password")]
        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "The password must be at least 8 characters")]
        public string Password { get; set; }

        [Display(Name = "Confirm password")]
        [Required(ErrorMessage = "Confirm your new password")]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "The passwords do not match")]
        public string ConfirmPassword { get; set; }
    }
}
