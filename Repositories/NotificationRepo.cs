using PTO_Management_System.Data;
using PTO_Management_System.Interfaces;
using PTO_Management_System.Models;

namespace PTO_Management_System.Repositories
{
    public class NotificationRepo : INotification
    {
        private readonly PtoDbContext _context;

        public NotificationRepo(PtoDbContext context)
        {
            _context = context;
        }

        public IQueryable<Notification> ForBeneficiary(int beneficiaryId)
        {
            return _context.Notifications
                .Where(n => n.BeneficiaryId == beneficiaryId)
                .OrderByDescending(n => n.DateSent);
        }

        public int UnreadCount(int beneficiaryId)
        {
            return _context.Notifications.Count(n => n.BeneficiaryId == beneficiaryId && !n.IsRead);
        }

        public Notification Add(Notification notification)
        {
            notification.DateSent = DateTime.UtcNow;
            _context.Notifications.Add(notification);
            _context.SaveChanges();
            return notification;
        }

        public void MarkAllRead(int beneficiaryId)
        {
            var unread = _context.Notifications.Where(n => n.BeneficiaryId == beneficiaryId && !n.IsRead).ToList();
            foreach (var n in unread) n.IsRead = true;
            _context.SaveChanges();
        }
    }
}
