using System.ComponentModel.DataAnnotations;

namespace PTO_Management_System.Models
{
    /// <summary>ERD entity SITE - an available settlement site that can be linked to a beneficiary (FR4).</summary>
    public class Site
    {
        [Key]
        public int Id { get; set; }

        [Display(Name = "Site number")]
        [Required(ErrorMessage = "Site number is required")]
        [StringLength(20)]
        [RegularExpression(@"^[A-Za-z0-9\-]+$", ErrorMessage = "Use letters, digits and dashes only, e.g. BS-0142")]
        public string SiteNumber { get; set; }

        [Display(Name = "Settlement")]
        [Required(ErrorMessage = "Settlement is required")]
        [StringLength(100)]
        public string Settlement { get; set; }

        [Display(Name = "Site size (m²)")]
        [Range(20, 5000, ErrorMessage = "Site size must be between 20 and 5000 m²")]
        public int SizeSqm { get; set; }

        [Display(Name = "Location / GPS")]
        [StringLength(100)]
        public string Location { get; set; }

        /// <summary>false = removed by the administrator (soft delete so history is kept).</summary>
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; }
    }

    /// <summary>One area with its capacity: total sites, free sites and the applications waiting for a decision.</summary>
    public class AreaRow
    {
        public Area Area { get; set; }
        public int TotalSites { get; set; }
        public int FreeSites { get; set; }
        public int Pending { get; set; }
    }

    /// <summary>Sites screen row: the site plus who occupies it.</summary>
    public class SiteRow
    {
        public Site Site { get; set; }
        public string Status { get; set; }          // Available or Occupied
        public string LinkedBeneficiary { get; set; }
    }
}
