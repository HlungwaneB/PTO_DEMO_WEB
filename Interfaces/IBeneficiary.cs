using PTO_Management_System.Models;

namespace PTO_Management_System.Interfaces
{
    public interface IBeneficiary
    {
        IQueryable<Beneficiary> GetAll();
        Beneficiary GetById(int id);
        Beneficiary ByUserId(string userId);
        Beneficiary ByIdNumber(string idNumber);

        /// <summary>Finds a beneficiary by ID number, cellphone or e-mail (what the user typed on the Log in screen).</summary>
        Beneficiary ByLoginIdentifier(string identifier);

        bool IdNumberExists(string idNumber);
        bool EmailExists(string email, int? exceptBeneficiaryId = null);
        bool CellphoneExists(string cellphone, int? exceptBeneficiaryId = null);

        Beneficiary Create(Beneficiary beneficiary);
        Beneficiary Edit(Beneficiary beneficiary);
        bool Delete(int id);
    }
}
