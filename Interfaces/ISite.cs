using PTO_Management_System.Models;

namespace PTO_Management_System.Interfaces
{
    public interface ISite
    {
        IQueryable<Site> GetAll();
        Site GetById(int id);
        bool SiteNumberExists(string siteNumber);
        Site Create(Site site);

        /// <summary>Soft delete. Returns false when the site is still occupied.</summary>
        bool Remove(int id);

        /// <summary>Active sites that no live PTO is using.</summary>
        List<Site> Available();

        /// <summary>Every area with its total / free sites and how many applications are waiting.</summary>
        List<AreaRow> Areas();

        /// <summary>Administrator: how many applications must be waiting before the system decides.</summary>
        void SetTarget(int areaId, int target);

        /// <summary>All active sites with their status (Available / Occupied) and linked beneficiary.</summary>
        List<SiteRow> Rows();
    }
}
