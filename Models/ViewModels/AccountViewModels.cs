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
}
