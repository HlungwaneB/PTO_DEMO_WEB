using System.ComponentModel.DataAnnotations;

namespace PTO_Management_System.Models
{
    /// <summary>A person living with the beneficiary (captured on the "Dependent information" screen).</summary>
    public class Dependant
    {
        [Key]
        public int Id { get; set; }
        public int BeneficiaryId { get; set; }
        public Beneficiary Beneficiary { get; set; }

        public string Surname { get; set; }
        public string Initials { get; set; }
        public string FirstNames { get; set; }
        public string Gender { get; set; }
        public string IdNumber { get; set; }
        public string Cellphone { get; set; }
        public string Email { get; set; }
    }
}
