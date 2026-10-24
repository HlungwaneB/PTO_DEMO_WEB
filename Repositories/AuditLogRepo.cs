using PTO_Management_System.Data;
using PTO_Management_System.Interfaces;
using PTO_Management_System.Models;

namespace PTO_Management_System.Repositories
{
    public class AuditLogRepo : IAuditLog
    {
        private readonly PtoDbContext _context;

        public AuditLogRepo(PtoDbContext context)
        {
            _context = context;
        }

        public IQueryable<AuditLog> GetAll()
        {
            return _context.AuditLogs.OrderByDescending(a => a.When);
        }

        public void Log(string userName, string action, string recordRef)
        {
            _context.AuditLogs.Add(new AuditLog
            {
                When = DateTime.UtcNow,
                UserName = userName,
                Action = action,
                RecordRef = recordRef
            });
            _context.SaveChanges();
        }
    }
}
