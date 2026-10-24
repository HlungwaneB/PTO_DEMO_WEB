using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PTO_Management_System.Models
{
    /// <summary>
    /// ERD entity USER (SRS 13.3). One row per beneficiary. The login itself (ID number + hashed password)
    /// lives in the ASP.NET Identity tables; UserId links the two.
    /// </summary>
    public class Beneficiary
    {
        [Key]
        public int Id { get; set; }

        // FK to AspNetUsers.Id (string GUID)
        public string UserId { get; set; }

        [Display(Name = "ID number")]
        [Required, StringLength(13, MinimumLength = 13)]
        public string IdNumber { get; set; }

        public string Title { get; set; }
        public string Initials { get; set; }
        public string Surname { get; set; }
        public string FirstNames { get; set; }
        public DateTime DateOfBirth { get; set; }
        public string Gender { get; set; }

        public string Cellphone { get; set; }
        public string HomeCellphone { get; set; }
        public string Email { get; set; }
        public string Address { get; set; }

        // Qualification answers captured on the "Reliable information" screen
        [Column(TypeName = "numeric(12,2)")]
        public decimal MonthlyIncome { get; set; }
        public bool IsSouthAfrican { get; set; }
        public bool IsFirstTimeOwner { get; set; }
        public bool IsOver21 { get; set; }
        public bool IsFirstTimeSubsidy { get; set; }
        public bool IsMilitaryVeteran { get; set; }
        public bool IsElderly { get; set; }

        public DateTime CreatedAt { get; set; }

        public List<Dependant> Dependants { get; set; } = new List<Dependant>();
        public List<PtoApplication> Applications { get; set; } = new List<PtoApplication>();
        public List<Notification> Notifications { get; set; } = new List<Notification>();

        [NotMapped]
        public string FullName => ((FirstNames ?? "") + " " + (Surname ?? "")).Trim();
    }
}
