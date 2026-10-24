using PTO_Management_System.Models;

namespace PTO_Management_System.Interfaces
{
    public interface IPtoDocument
    {
        IQueryable<PtoDocument> ForApplication(int applicationId);
        PtoDocument GetById(int id);
        PtoDocument Add(PtoDocument document);
        PtoDocument Remove(int id);
        void SetVerification(int id, string status, string by);
    }
}
