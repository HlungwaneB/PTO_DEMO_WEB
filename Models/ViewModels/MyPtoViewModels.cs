using System.ComponentModel.DataAnnotations;
using PTO_Management_System.Validation;

namespace PTO_Management_System.Models
{
    /// <summary>Screen 05 - Home (beneficiary).</summary>
    public class HomeViewModel
    {
        public Beneficiary Beneficiary { get; set; }
        public PtoApplication Application { get; set; }
        public int UnreadNotifications { get; set; }
        public bool ShowExpiryAlert { get; set; }
    }

    /// <summary>One step of the Track PTO timeline: state is done, now or next.</summary>
    public class TrackStep
    {
        public string Title { get; set; }
        public string Detail { get; set; }
        public string State { get; set; } = "next";
    }

    public class TrackViewModel
    {
        public PtoApplication Application { get; set; }
        public List<TrackStep> Steps { get; set; } = new List<TrackStep>();
        public int PathwayStage { get; set; }   // 0-5, drives the 5-segment formalisation bar
        public string PathwayText { get; set; }
    }

    /// <summary>Screen 16 - Renewal request.</summary>
    public class RenewalViewModel
    {
        public PtoApplication Application { get; set; }

        [Display(Name = "Still living at the site")]
        [Required(ErrorMessage = "Please answer YES or NO")]
        public bool? StillLiving { get; set; }

        [Display(Name = "Change in income or household")]
        [Required(ErrorMessage = "Please answer YES or NO")]
        public bool? HouseholdChanged { get; set; }

        [Display(Name = "New monthly income")]
        [Range(0, 1000000, ErrorMessage = "Enter a valid amount")]
        public decimal? NewIncome { get; set; }

        [StringLength(500, ErrorMessage = "Notes may be at most 500 characters")]
        public string Notes { get; set; }
    }

    /// <summary>De-register screen: the beneficiary gives a reason.</summary>
    public class DeregisterViewModel
    {
        public PtoApplication Application { get; set; }

        [Required(ErrorMessage = "Give a reason")]
        [StringLength(300, MinimumLength = 5, ErrorMessage = "The reason must be 5 to 300 characters")]
        public string Reason { get; set; }
    }

    /// <summary>Apply again for another area after a rejection.</summary>
    public class ReapplyViewModel
    {
        public PtoApplication Application { get; set; }
        public List<AreaRow> Areas { get; set; } = new List<AreaRow>();

        [Required(ErrorMessage = "Choose an area")]
        public string Settlement { get; set; }
    }

    /// <summary>Screen 18 - Update personal information.</summary>
    public class ProfileViewModel
    {
        public string FullName { get; set; }
        public string IdNumber { get; set; }
        public int DependantsCount { get; set; }

        [Required(ErrorMessage = "Cellphone is required")]
        [SaCellphone]
        public string Cellphone { get; set; }

        [Display(Name = "E-mail address")]
        [Required(ErrorMessage = "E-mail address is required")]
        [EmailAddress(ErrorMessage = "Enter a valid e-mail address")]
        public string Email { get; set; }

        [StringLength(200, ErrorMessage = "Address is too long")]
        public string Address { get; set; }
    }
}
