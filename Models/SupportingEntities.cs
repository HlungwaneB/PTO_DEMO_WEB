using System.ComponentModel.DataAnnotations;

namespace PTO_Management_System.Models
{
    /// <summary>FR15 - every status change on a PTO is recorded here (record history).</summary>
    /// <summary>
    /// An area (settlement) with a LIMITED number of sites. The system collects applications until
    /// ApplicationTarget is reached, then ranks the waiting applicants and allocates the free sites automatically.
    /// </summary>
    public class Area
    {
        [Key]
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; }

        /// <summary>How many applications must be waiting before the system ranks them and decides.</summary>
        [Range(1, 1000, ErrorMessage = "Enter a number between 1 and 1000")]
        public int ApplicationTarget { get; set; } = 5;
    }

    public class PtoStatusHistory
    {
        [Key]
        public int Id { get; set; }
        public int PtoApplicationId { get; set; }
        public PtoApplication PtoApplication { get; set; }
        public string FromStatus { get; set; }
        public string ToStatus { get; set; }
        public string ChangedBy { get; set; }
        public DateTime ChangedAt { get; set; }
        public string Note { get; set; }
    }

    /// <summary>ERD entity DOCUMENT - uploaded supporting document (certified ID, proof of income ...).</summary>
    public class PtoDocument
    {
        [Key]
        public int Id { get; set; }
        public int PtoApplicationId { get; set; }
        public PtoApplication PtoApplication { get; set; }
        public string DocumentType { get; set; }
        public string OriginalFileName { get; set; }
        /// <summary>Random file name used in storage (never the user's file name).</summary>
        public string StoredFileName { get; set; }
        public string ContentType { get; set; }
        public long SizeBytes { get; set; }
        public DateTime UploadedAt { get; set; }
        /// <summary>Uploaded, Verified or Rejected</summary>
        public string VerificationStatus { get; set; } = "Uploaded";
    }

    /// <summary>ERD entity NOTIFICATION (FR9 expiry reminders, status updates).</summary>
    public class Notification
    {
        [Key]
        public int Id { get; set; }
        public int BeneficiaryId { get; set; }
        public Beneficiary Beneficiary { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        /// <summary>info, success or warning (colours the dot on the Notifications screen)</summary>
        public string Kind { get; set; } = "info";
        public DateTime DateSent { get; set; }
        public bool IsRead { get; set; }
    }

    /// <summary>FR10 - a beneficiary asks for the PTO to be renewed.</summary>
    public class RenewalRequest
    {
        [Key]
        public int Id { get; set; }
        public int PtoApplicationId { get; set; }
        public PtoApplication PtoApplication { get; set; }
        public bool StillLivingOnSite { get; set; }
        public bool HouseholdChanged { get; set; }
        public string Notes { get; set; }
        public DateTime RequestedAt { get; set; }
        /// <summary>Pending, Approved or Declined</summary>
        public string Status { get; set; } = "Pending";
    }

    /// <summary>ERD entity REPORT - reports filed by the administrator (CSV text is kept in Content).</summary>
    public class Report
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; }
        public string Period { get; set; }
        public DateTime GeneratedAt { get; set; }
        public string GeneratedBy { get; set; }
        public string Content { get; set; }
    }

    /// <summary>Audit trail screen (SRS 5.1 "Audit trail": who changed what, and when).</summary>
    public class AuditLog
    {
        [Key]
        public int Id { get; set; }
        public DateTime When { get; set; }
        public string UserName { get; set; }
        public string Action { get; set; }
        public string RecordRef { get; set; }
    }
}
