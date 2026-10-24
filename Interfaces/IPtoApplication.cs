using PTO_Management_System.Models;

namespace PTO_Management_System.Interfaces
{
    public interface IPtoApplication
    {
        /// <summary>All applications with Beneficiary and Site loaded (read only list queries).</summary>
        IQueryable<PtoApplication> GetAll();

        /// <summary>One application with documents, history, renewals, beneficiary (+dependants) and site.</summary>
        PtoApplication GetById(int id);
        PtoApplication ByBeneficiaryId(int beneficiaryId);
        PtoApplication Create(PtoApplication application);

        // ---- workflow: the SYSTEM decides, staff do not approve (every call writes history + audit log) ----

        /// <summary>
        /// Submit: automatically verifies the documents and the eligibility rules, scores the applicant and, when the
        /// area has reached its application target, ranks the waiting applicants and allocates the free sites.
        /// The result is Pending, Active (site allocated) or Rejected.
        /// </summary>
        void Submit(int id, string by);

        /// <summary>The beneficiary renews. The system decides at once and returns the request (Approved or Declined).</summary>
        RenewalRequest Renew(int id, bool stillLiving, bool householdChanged, decimal? newIncome, string notes);

        /// <summary>The beneficiary de-registers (releases the site) or withdraws a pending application.</summary>
        void Deregister(int id, string reason, string by);

        /// <summary>A rejected (or de-registered) applicant applies again for another area. Documents are kept; the application becomes a Draft again.</summary>
        void Reapply(int id, string settlement);

        /// <summary>Decide at once if the area already has enough waiting applications (used when the target is lowered).</summary>
        void RunAllocationFor(string settlement);

        bool SiteIsOccupied(int siteId);
    }
}
