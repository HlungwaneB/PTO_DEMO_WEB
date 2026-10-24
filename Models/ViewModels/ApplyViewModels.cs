using System.ComponentModel.DataAnnotations;
using PTO_Management_System.Validation;

namespace PTO_Management_System.Models
{
    /// <summary>Screen 09 - Reliable information (eligibility questions).</summary>
    public class EligibilityViewModel
    {
        [Display(Name = "Monthly income (less than R3 500)")]
        [Required(ErrorMessage = "Enter your monthly income")]
        [Range(0, 1000000, ErrorMessage = "Enter a valid amount")]
        public decimal? MonthlyIncome { get; set; }

        [Display(Name = "South African / Naturalised")]
        [Required(ErrorMessage = "Please answer YES or NO")]
        public bool? IsSouthAfrican { get; set; }

        [Display(Name = "First-time home owner")]
        [Required(ErrorMessage = "Please answer YES or NO")]
        public bool? IsFirstTimeOwner { get; set; }

        [Display(Name = "Over 21")]
        [Required(ErrorMessage = "Please answer YES or NO")]
        public bool? IsOver21 { get; set; }

        [Display(Name = "First-time government subsidy recipient")]
        [Required(ErrorMessage = "Please answer YES or NO")]
        public bool? IsFirstTimeSubsidy { get; set; }

        [Display(Name = "Military veteran (optional)")]
        public bool IsMilitaryVeteran { get; set; }

        [Display(Name = "Elderly person")]
        public bool IsElderly { get; set; }

        /// <summary>The five compulsory requirements shown on the "Not eligible" screen.</summary>
        public bool Qualifies()
        {
            return MonthlyIncome.HasValue && MonthlyIncome.Value < WebConstants.MaxMonthlyIncome
                && IsSouthAfrican == true && IsFirstTimeOwner == true
                && IsOver21 == true && IsFirstTimeSubsidy == true;
        }
    }

    /// <summary>Screen 10 - Personal details and ID number.</summary>
    public class PersonalViewModel : IValidatableObject
    {
        [Required(ErrorMessage = "Surname is required")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Surname must be 2 to 50 characters")]
        [RegularExpression(NameRules.Pattern, ErrorMessage = NameRules.Message)]
        public string Surname { get; set; }

        [Required(ErrorMessage = "Initials are required")]
        [StringLength(10, ErrorMessage = "Initials are too long")]
        [RegularExpression(@"^[A-Za-z][A-Za-z\s\.]*$", ErrorMessage = "Letters only, e.g. A L")]
        public string Initials { get; set; }

        [Required(ErrorMessage = "Select a title")]
        public string Title { get; set; }

        [Display(Name = "First name(s)")]
        [Required(ErrorMessage = "First name(s) are required")]
        [StringLength(80, MinimumLength = 2, ErrorMessage = "First name must be 2 to 80 characters")]
        [RegularExpression(NameRules.Pattern, ErrorMessage = NameRules.Message)]
        public string FirstNames { get; set; }

        [Display(Name = "Date of Birth")]
        [Required(ErrorMessage = "Date of birth is required")]
        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }

        [Display(Name = "ID number")]
        [Required(ErrorMessage = "ID number is required")]
        [SouthAfricanId]
        public string IdNumber { get; set; }

        [Required(ErrorMessage = "Select your gender")]
        public string Gender { get; set; }

        /// <summary>Cross-checks that the ID number, date of birth and gender agree with each other.</summary>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!SaIdValidator.TryParse(IdNumber, out var idDob, out var idGender, out var citizen))
                yield break; // the [SouthAfricanId] attribute already reported the problem

            if (!citizen)
                yield return new ValidationResult(
                    "This ID number belongs to a permanent resident. Only South African citizens can apply.",
                    new[] { nameof(IdNumber) });

            if (DateOfBirth.HasValue && DateOfBirth.Value.Date != idDob.Date)
                yield return new ValidationResult(
                    "The date of birth does not match the ID number.", new[] { nameof(DateOfBirth) });

            if (!string.IsNullOrEmpty(Gender) && Gender != idGender)
                yield return new ValidationResult(
                    "The gender does not match the ID number.", new[] { nameof(Gender) });

            if (SaIdValidator.AgeOn(idDob, DateTime.UtcNow) < 21)
                yield return new ValidationResult(
                    "You must be over 21 to apply.", new[] { nameof(IdNumber) });
        }
    }

    /// <summary>Screen 11 - Contact details.</summary>
    public class ContactViewModel
    {
        [Required(ErrorMessage = "Cellphone is required")]
        [SaCellphone]
        public string Cellphone { get; set; }

        [Display(Name = "Confirm cellphone")]
        [Required(ErrorMessage = "Confirm your cellphone number")]
        [Compare(nameof(Cellphone), ErrorMessage = "The cellphone numbers do not match")]
        public string ConfirmCellphone { get; set; }

        [Display(Name = "E-mail address")]
        [Required(ErrorMessage = "E-mail address is required")]
        [EmailAddress(ErrorMessage = "Enter a valid e-mail address")]
        [StringLength(100)]
        public string Email { get; set; }

        [Display(Name = "Confirm e-mail address")]
        [Required(ErrorMessage = "Confirm your e-mail address")]
        [Compare(nameof(Email), ErrorMessage = "The e-mail addresses do not match")]
        public string ConfirmEmail { get; set; }

        [Display(Name = "Area you are applying for")]
        [Required(ErrorMessage = "Choose the area you are applying for")]
        public string Settlement { get; set; }

        [Display(Name = "Home cellphone")]
        [SaCellphone]
        public string HomeCellphone { get; set; }

        [Display(Name = "Do you have dependants?")]
        [Required(ErrorMessage = "Please answer YES or NO")]
        public bool? HasDependants { get; set; }
    }

    /// <summary>Screen 19 - Dependent information (one dependant at a time).</summary>
    public class DependantViewModel
    {
        [Required(ErrorMessage = "Surname is required")]
        [StringLength(50, MinimumLength = 2)]
        [RegularExpression(NameRules.Pattern, ErrorMessage = NameRules.Message)]
        public string Surname { get; set; }

        [Required(ErrorMessage = "Initials are required")]
        [StringLength(10)]
        [RegularExpression(@"^[A-Za-z][A-Za-z\s\.]*$", ErrorMessage = "Letters only")]
        public string Initials { get; set; }

        [Display(Name = "First name(s)")]
        [Required(ErrorMessage = "First name(s) are required")]
        [StringLength(80, MinimumLength = 2)]
        [RegularExpression(NameRules.Pattern, ErrorMessage = NameRules.Message)]
        public string FirstNames { get; set; }

        [Required(ErrorMessage = "Select a gender")]
        public string Gender { get; set; }

        [Display(Name = "ID number")]
        [SouthAfricanId] // optional: young children may not have an ID number yet
        public string IdNumber { get; set; }

        [SaCellphone]
        public string Cellphone { get; set; }

        [Display(Name = "E-mail address")]
        [EmailAddress(ErrorMessage = "Enter a valid e-mail address")]
        public string Email { get; set; }

        public bool HasAnyData =>
            !string.IsNullOrWhiteSpace(Surname) || !string.IsNullOrWhiteSpace(Initials) ||
            !string.IsNullOrWhiteSpace(FirstNames) || !string.IsNullOrWhiteSpace(IdNumber) ||
            !string.IsNullOrWhiteSpace(Cellphone) || !string.IsNullOrWhiteSpace(Email);
    }

    /// <summary>Page model for the Dependants screen: the form plus the dependants already added.</summary>
    public class DependantsPageViewModel
    {
        public DependantViewModel Form { get; set; } = new DependantViewModel();
        public List<DependantViewModel> Added { get; set; } = new List<DependantViewModel>();
    }

    /// <summary>NEW screen - Create Password (after dependants, or straight after Contact when there are none).</summary>
    public class CreatePasswordViewModel
    {
        /// <summary>Shown read-only: the ID number is the reference / user name.</summary>
        public string IdNumber { get; set; }

        /// <summary>Only used so the Back button returns to the right page.</summary>
        public bool HasDependants { get; set; }

        [Required(ErrorMessage = "Create a password")]
        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "The password must be at least 8 characters")]
        public string Password { get; set; }

        [Display(Name = "Confirm Password")]
        [Required(ErrorMessage = "Confirm your password")]
        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "The passwords do not match")]
        public string ConfirmPassword { get; set; }
    }

    /// <summary>Screen 12 - Required documents.</summary>
    public class DocumentRow
    {
        public string DocumentType { get; set; }
        public bool Required { get; set; }
        public PtoDocument Uploaded { get; set; }
    }

    public class DocumentsPageViewModel
    {
        public List<DocumentRow> Rows { get; set; } = new List<DocumentRow>();
        public bool AllRequiredUploaded => Rows.All(r => !r.Required || r.Uploaded != null);
    }

    /// <summary>Screen 13 - Review and submit.</summary>
    public class ReviewViewModel
    {
        public Beneficiary Beneficiary { get; set; }
        public PtoApplication Application { get; set; }
        public int DocumentsAttached { get; set; }
        public int DocumentsRequired { get; set; }
        public int RequirementsMet { get; set; }
    }
}
