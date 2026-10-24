using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PTO_Management_System.Models
{
    /// <summary>
    /// ERD entity PTO_APPLICATION. The ReferenceNumber IS the beneficiary's ID number
    /// (requirement from the team: "the reference must be their ID").
    /// </summary>
    public class PtoApplication
    {
        [Key]
        public int Id { get; set; }

        public int BeneficiaryId { get; set; }
        public Beneficiary Beneficiary { get; set; }

        public int? SiteId { get; set; }
        public Site Site { get; set; }

        [Display(Name = "Reference number")]
        public string ReferenceNumber { get; set; }

        /// <summary>Official PTO number, generated when the administrator approves (e.g. PTO-2026-00012).</summary>
        /// <summary>The area (settlement) the applicant applied for. Its sites are limited.</summary>
        [Display(Name = "Area")]
        public string Settlement { get; set; }

        /// <summary>Need score worked out when the application is submitted (lower income, veteran, elderly, dependants = higher).</summary>
        public int Score { get; set; }
        public string ScoreDetail { get; set; }

        [Display(Name = "PTO number")]
        public string PtoNumber { get; set; }

        public string Status { get; set; } = PtoStatus.Draft;

        public DateTime CreatedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }

        [Display(Name = "Issued")]
        public DateTime? IssueDate { get; set; }

        [Display(Name = "Valid until")]
        public DateTime? ExpiryDate { get; set; }

        public DateTime? DeregisteredAt { get; set; }
        public string DeregisterReason { get; set; }
        public string RejectReason { get; set; }

        public List<PtoDocument> Documents { get; set; } = new List<PtoDocument>();
        public List<PtoStatusHistory> History { get; set; } = new List<PtoStatusHistory>();
        public List<RenewalRequest> Renewals { get; set; } = new List<RenewalRequest>();

        [NotMapped]
        public int? DaysToExpiry => ExpiryDate.HasValue
            ? (int?)(ExpiryDate.Value.Date - DateTime.UtcNow.Date).TotalDays
            : null;
    }
}
