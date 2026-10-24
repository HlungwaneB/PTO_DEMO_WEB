using PTO_Management_System.Models;

namespace PTO_Management_System.Interfaces
{
    public interface INotification
    {
        IQueryable<Notification> ForBeneficiary(int beneficiaryId);
        int UnreadCount(int beneficiaryId);
        Notification Add(Notification notification);
        void MarkAllRead(int beneficiaryId);
    }
}
