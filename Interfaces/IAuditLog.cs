using PTO_Management_System.Models;

namespace PTO_Management_System.Interfaces
{
    public interface IAuditLog
    {
        IQueryable<AuditLog> GetAll();
        void Log(string userName, string action, string recordRef);
    }
}
