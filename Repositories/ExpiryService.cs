using Microsoft.EntityFrameworkCore;
using PTO_Management_System.Data;
using PTO_Management_System.Interfaces;
using PTO_Management_System.Models;

namespace PTO_Management_System.Repositories
{
    /// <summary>
    /// FR8 + FR9: calculates the six-month validity and sends reminders.
    ///   more than 30 days left  -> stays Active / Renewed
    ///   30 days or less left    -> Expiring  (+ reminder notification)
    ///   past the expiry date    -> Expired   (+ notification)
    /// Real SMS needs a paid provider (risk R07), so the SMS is simulated in the log.
    /// </summary>
    public class ExpiryService : IExpiryService
    {
        private readonly PtoDbContext _context;
        private readonly ILogger<ExpiryService> _logger;

        public ExpiryService(PtoDbContext context, ILogger<ExpiryService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public int Refresh()
        {
            var monitored = PtoStatus.Monitored;
            var today = DateTime.UtcNow.Date;
            int changed = 0;

            var live = _context.PtoApplications
                .Include(a => a.Beneficiary)
                .Where(a => a.ExpiryDate != null && (monitored.Contains(a.Status)))
                .ToList();

            foreach (var app in live)
            {
                int daysLeft = (int)(app.ExpiryDate.Value.Date - today).TotalDays;

                string target = null;
                if (daysLeft < 0) target = PtoStatus.Expired;
                else if (daysLeft <= WebConstants.ExpiryWarningDays) target = PtoStatus.Expiring;
                // Extra reminders while Expiring: 7, 3 and 1 day(s) before expiry (once per day)
                if (app.Status == PtoStatus.Expiring && (daysLeft == 7 || daysLeft == 3 || daysLeft == 1))
                {
                    string reminderTitle = "Expiry reminder: " + daysLeft + (daysLeft == 1 ? " day left" : " days left");
                    bool already = _context.Notifications.Any(n => n.BeneficiaryId == app.BeneficiaryId
                                                                && n.Title == reminderTitle && n.DateSent >= today);
                    if (!already)
                    {
                        _context.Notifications.Add(new Notification
                        {
                            BeneficiaryId = app.BeneficiaryId,
                            Title = reminderTitle,
                            Message = "Your PTO expires on " + app.ExpiryDate.Value.ToString("dd MMM yyyy")
                                      + ". Request a renewal to stay covered.",
                            Kind = "warning",
                            DateSent = DateTime.UtcNow
                        });
                        changed++;
                    }
                }



                if (target == null || target == app.Status) continue;

                _context.PtoStatusHistories.Add(new PtoStatusHistory
                {
                    PtoApplicationId = app.Id,
                    FromStatus = app.Status,
                    ToStatus = target,
                    ChangedBy = "System",
                    ChangedAt = DateTime.UtcNow,
                    Note = "Automatic expiry check"
                });
                app.Status = target;

                string message = target == PtoStatus.Expiring
                    ? "Your PTO expires on " + app.ExpiryDate.Value.ToString("dd MMM yyyy") + " (" + daysLeft + " days). Request a renewal to stay covered."
                    : "Your PTO expired on " + app.ExpiryDate.Value.ToString("dd MMM yyyy") + ". Please request a renewal.";

                _context.Notifications.Add(new Notification
                {
                    BeneficiaryId = app.BeneficiaryId,
                    Title = target == PtoStatus.Expiring ? "Expiry reminder" : "PTO expired",
                    Message = message,
                    Kind = "warning",
                    DateSent = DateTime.UtcNow
                });

                _context.AuditLogs.Add(new AuditLog
                {
                    When = DateTime.UtcNow,
                    UserName = "System",
                    Action = "Status -> " + target,
                    RecordRef = app.PtoNumber ?? app.ReferenceNumber
                });

                _logger.LogInformation("SMS (simulated) to {Phone}: {Message}", app.Beneficiary?.Cellphone, message);
                changed++;
            }

            if (changed > 0) _context.SaveChanges();
            return changed;
        }
    }
}
