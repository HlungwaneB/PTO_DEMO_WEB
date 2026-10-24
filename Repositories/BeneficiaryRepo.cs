using Microsoft.EntityFrameworkCore;
using PTO_Management_System.Data;
using PTO_Management_System.Interfaces;
using PTO_Management_System.Models;
using PTO_Management_System.Validation;

namespace PTO_Management_System.Repositories
{
    public class BeneficiaryRepo : IBeneficiary
    {
        private readonly PtoDbContext _context;

        public BeneficiaryRepo(PtoDbContext context)
        {
            _context = context;
        }

        public IQueryable<Beneficiary> GetAll()
        {
            return _context.Beneficiaries.Include(b => b.Applications);
        }

        public Beneficiary GetById(int id)
        {
            return _context.Beneficiaries.Include(b => b.Dependants).FirstOrDefault(b => b.Id == id);
        }

        public Beneficiary ByUserId(string userId)
        {
            return _context.Beneficiaries.Include(b => b.Dependants).FirstOrDefault(b => b.UserId == userId);
        }

        public Beneficiary ByIdNumber(string idNumber)
        {
            return _context.Beneficiaries.Include(b => b.Dependants).FirstOrDefault(b => b.IdNumber == idNumber);
        }

        public Beneficiary ByLoginIdentifier(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier)) return null;
            var text = identifier.Trim();

            // 1. e-mail address
            if (text.Contains("@"))
            {
                var email = text.ToLower();
                return _context.Beneficiaries.FirstOrDefault(b => b.Email.ToLower() == email);
            }

            // 2. 13-digit ID number (spaces are ignored)
            var digits = new string(text.Where(char.IsDigit).ToArray());
            if (digits.Length == 13)
            {
                var byId = _context.Beneficiaries.FirstOrDefault(b => b.IdNumber == digits);
                if (byId != null) return byId;
            }

            // 3. cellphone number
            var phone = PhoneHelper.Normalize(text);
            if (phone != null)
                return _context.Beneficiaries.FirstOrDefault(b => b.Cellphone == phone);

            return null;
        }

        public bool IdNumberExists(string idNumber)
        {
            return _context.Beneficiaries.Any(b => b.IdNumber == idNumber);
        }

        public bool EmailExists(string email, int? exceptBeneficiaryId = null)
        {
            var lower = (email ?? "").Trim().ToLower();
            return _context.Beneficiaries.Any(b => b.Email.ToLower() == lower
                && (exceptBeneficiaryId == null || b.Id != exceptBeneficiaryId));
        }

        public bool CellphoneExists(string cellphone, int? exceptBeneficiaryId = null)
        {
            var normalised = PhoneHelper.Normalize(cellphone);
            if (normalised == null) return false;
            return _context.Beneficiaries.Any(b => b.Cellphone == normalised
                && (exceptBeneficiaryId == null || b.Id != exceptBeneficiaryId));
        }

        public Beneficiary Create(Beneficiary beneficiary)
        {
            beneficiary.CreatedAt = DateTime.UtcNow;
            _context.Beneficiaries.Add(beneficiary);
            _context.SaveChanges();
            return beneficiary;
        }

        public Beneficiary Edit(Beneficiary beneficiary)
        {
            _context.Beneficiaries.Update(beneficiary);
            _context.SaveChanges();
            return beneficiary;
        }

        public bool Delete(int id)
        {
            var beneficiary = _context.Beneficiaries.FirstOrDefault(b => b.Id == id);
            if (beneficiary == null) return false;
            _context.Beneficiaries.Remove(beneficiary);
            _context.SaveChanges();
            return true;
        }
    }
}
